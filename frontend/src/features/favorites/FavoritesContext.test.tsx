import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { FavoritesProvider } from './FavoritesContext'
import { useFavorites } from './useFavorites'
import { FAVORITES_KEY, type FavoriteStorage } from './storage'

function Probe() {
  const favorites = useFavorites()
  return (
    <>
      <span data-testid="ids">{favorites.ids.join(',')}</span>
      <span data-testid="notice">{favorites.notice}</span>
      <button
        onClick={() =>
          favorites.addFavorite('med-par-500', 'Paracetamol 500 mg')
        }
      >
        Add
      </button>
      <button
        onClick={() =>
          favorites.removeFavorite('med-par-500', 'Paracetamol 500 mg')
        }
      >
        Remove
      </button>
      <button
        onClick={() =>
          favorites.toggleFavorite('med-cet-10', 'Cetirizine 10 mg')
        }
      >
        Toggle
      </button>
      <button onClick={() => favorites.clearFavorites()}>Clear</button>
    </>
  )
}
import { memoryStorage } from '../../test/favoriteStorage'
function mount(storage: FavoriteStorage | null) {
  return render(
    <FavoritesProvider storage={storage}>
      <Probe />
    </FavoritesProvider>,
  )
}

describe('favorites ownership and cross-tab updates', () => {
  it('adds, removes, toggles, prevents duplicates, and announces concise changes', async () => {
    const storage = memoryStorage()
    const view = mount(storage)
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))
    expect(screen.getByTestId('ids')).toHaveTextContent('med-par-500')
    expect(storage.setItem).toHaveBeenCalledTimes(1)
    expect(
      view.container.querySelector('[aria-live="polite"]'),
    ).toHaveTextContent('Bookmark saved: Paracetamol 500 mg.')
    await userEvent.click(screen.getByRole('button', { name: 'Toggle' }))
    expect(screen.getByTestId('ids')).toHaveTextContent(
      'med-par-500,med-cet-10',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }))
    expect(screen.getByTestId('ids')).toHaveTextContent('med-cet-10')
    await userEvent.click(screen.getByRole('button', { name: 'Toggle' }))
    expect(screen.getByTestId('ids')).toBeEmptyDOMElement()
  })
  it('retains bookmarks across provider remount and clears only its storage key', async () => {
    const storage = memoryStorage()
    storage.setItem('theme', 'dark')
    let view = mount(storage)
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))
    view.unmount()
    view = mount(storage)
    expect(screen.getByTestId('ids')).toHaveTextContent('med-par-500')
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(storage.getItem(FAVORITES_KEY)).toBeNull()
    expect(storage.getItem('theme')).toBe('dark')
    view.unmount()
  })
  it.each(['{broken', '{"version":7,"medicationIds":[]}'])(
    'does not overwrite unreadable initial payload %s',
    (raw) => {
      const storage = memoryStorage(raw)
      mount(storage)
      expect(screen.getByTestId('ids')).toBeEmptyDOMElement()
      expect(storage.setItem).not.toHaveBeenCalled()
      expect(storage.removeItem).not.toHaveBeenCalled()
    },
  )
  it.each(['unavailable', 'read', 'write'] as const)(
    'continues in memory after %s failure',
    async (failure) => {
      const storage = memoryStorage()
      if (failure === 'read')
        storage.getItem.mockImplementation(() => {
          throw new Error('read')
        })
      if (failure === 'write')
        storage.setItem.mockImplementation(() => {
          throw new Error('quota')
        })
      mount(failure === 'unavailable' ? null : storage)
      if (failure !== 'write')
        expect(screen.getByTestId('notice')).toHaveTextContent('unavailable')
      await userEvent.click(screen.getByRole('button', { name: 'Add' }))
      expect(screen.getByTestId('ids')).toHaveTextContent('med-par-500')
      if (failure !== 'read')
        expect(screen.getByTestId('notice')).toHaveTextContent('unavailable')
    },
  )
  it('validates relevant events, ignores unrelated events, and never echoes writes', () => {
    const storage = memoryStorage()
    mount(storage)
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', { key: 'theme', newValue: 'dark' }),
      ),
    )
    expect(screen.getByTestId('ids')).toBeEmptyDOMElement()
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: FAVORITES_KEY,
          newValue: '{"version":1,"medicationIds":["med-cet-10","med-cet-10"]}',
        }),
      ),
    )
    expect(screen.getByTestId('ids')).toHaveTextContent('med-cet-10')
    expect(storage.setItem).not.toHaveBeenCalled()
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', { key: FAVORITES_KEY, newValue: 'broken' }),
      ),
    )
    expect(screen.getByTestId('ids')).toBeEmptyDOMElement()
    expect(screen.getByTestId('notice')).toHaveTextContent('invalid')
  })
  it('handles key removal and browser storage clear events', () => {
    const storage = memoryStorage(
      '{"version":1,"medicationIds":["med-par-500"]}',
    )
    mount(storage)
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', { key: FAVORITES_KEY, newValue: null }),
      ),
    )
    expect(screen.getByTestId('ids')).toBeEmptyDOMElement()
    act(() =>
      window.dispatchEvent(
        new StorageEvent('storage', { key: null, newValue: null }),
      ),
    )
    expect(storage.removeItem).not.toHaveBeenCalled()
  })
})
