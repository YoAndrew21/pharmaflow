import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DiscoveryCarousel } from './DiscoveryCarousel'

afterEach(() => vi.unstubAllGlobals())

function setup(reduced = false) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: reduced })),
  )
  render(
    <DiscoveryCarousel
      title="Demo presentations"
      description="Fictional catalog"
      items={[
        {
          id: 'one',
          content: <a href="/medications/one">First presentation</a>,
        },
        { id: 'two', content: <button>Save second presentation</button> },
        {
          id: 'three',
          content: <a href="/medications/three">Last presentation</a>,
        },
      ]}
    />,
  )
  const list = screen.getByRole('list', { name: 'Demo presentations' })
  Object.defineProperties(list, {
    clientWidth: { configurable: true, value: 400 },
    scrollWidth: { configurable: true, value: 1200 },
  })
  const update = (left: number) => {
    list.scrollLeft = Math.max(0, Math.min(800, left))
    fireEvent.scroll(list)
  }
  list.scrollBy = vi.fn((options: ScrollToOptions) =>
    update(list.scrollLeft + (options.left ?? 0)),
  ) as typeof list.scrollBy
  list.scrollTo = vi.fn((options: ScrollToOptions) =>
    update(options.left ?? 0),
  ) as typeof list.scrollTo
  fireEvent.scroll(list)
  return {
    list,
    previous: screen.getByRole('button', { name: 'Previous presentations' }),
    next: screen.getByRole('button', { name: 'Next presentations' }),
  }
}

describe('native discovery carousel controls', () => {
  it('names the list, explains keyboard use and connects both controls to it', () => {
    const { list, previous, next } = setup()
    expect(list).toHaveAccessibleDescription(/Left\/Right or Home\/End/)
    expect(list).toHaveAttribute('tabindex', '0')
    expect(previous).toHaveAttribute('aria-controls', list.id)
    expect(next).toHaveAttribute('aria-controls', list.id)
    expect(previous).toHaveAttribute('aria-disabled', 'true')
    expect(next).toHaveAttribute('aria-disabled', 'false')
  })
  it('moves in both directions, stops at the ends and keeps the control focused', async () => {
    const { list, previous, next } = setup()
    await userEvent.click(previous)
    expect(list.scrollBy).not.toHaveBeenCalled()
    await userEvent.click(next)
    expect(list.scrollLeft).toBeGreaterThan(0)
    expect(previous).toHaveAttribute('aria-disabled', 'false')
    await userEvent.click(next)
    await userEvent.click(next)
    expect(next).toHaveAttribute('aria-disabled', 'true')
    expect(next).toHaveFocus()
    const calls = vi.mocked(list.scrollBy).mock.calls.length
    await userEvent.click(next)
    expect(list.scrollBy).toHaveBeenCalledTimes(calls)
    await userEvent.click(previous)
    expect(list.scrollLeft).toBeLessThan(800)
  })
  it('supports list arrow/Home/End keys without intercepting nested controls', async () => {
    const { list } = setup()
    list.focus()
    await userEvent.keyboard('{ArrowRight}')
    expect(list.scrollLeft).toBeGreaterThan(0)
    await userEvent.keyboard('{End}')
    expect(list.scrollLeft).toBe(800)
    await userEvent.keyboard('{Home}')
    expect(list.scrollLeft).toBe(0)
    expect(list).toHaveFocus()
    await userEvent.tab()
    expect(
      screen.getByRole('link', { name: 'First presentation' }),
    ).toHaveFocus()
    const calls = vi.mocked(list.scrollBy).mock.calls.length
    await userEvent.keyboard('{ArrowRight}')
    expect(list.scrollBy).toHaveBeenCalledTimes(calls)
    await userEvent.tab()
    expect(
      screen.getByRole('button', { name: 'Save second presentation' }),
    ).toHaveFocus()
  })
  it.each([
    [false, 'smooth'],
    [true, 'instant'],
  ] as const)(
    'honors reduced motion = %s when moving',
    async (reduced, behavior) => {
      const { list, next } = setup(reduced)
      await userEvent.click(next)
      expect(list.scrollBy).toHaveBeenCalledWith(
        expect.objectContaining({ behavior }),
      )
      list.focus()
      await userEvent.keyboard('{End}')
      expect(list.scrollTo).toHaveBeenCalledWith(
        expect.objectContaining({ behavior }),
      )
    },
  )
  it('updates controls when resizing makes the complete list visible', () => {
    const { list, previous, next } = setup()
    Object.defineProperty(list, 'clientWidth', { value: 1200 })
    act(() => window.dispatchEvent(new Event('resize')))
    expect(previous).toHaveAttribute('aria-disabled', 'true')
    expect(next).toHaveAttribute('aria-disabled', 'true')
  })
  it('brings a partially visible card into view when one of its controls receives focus', () => {
    const { list } = setup()
    const link = screen.getByRole('link', { name: 'Last presentation' })
    const item = link.closest('li')!
    vi.spyOn(list, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      right: 400,
    } as DOMRect)
    vi.spyOn(item, 'getBoundingClientRect').mockReturnValue({
      left: 380,
      right: 700,
    } as DOMRect)
    item.scrollIntoView = vi.fn()
    act(() => link.focus())
    expect(item.scrollIntoView).toHaveBeenCalledWith({
      behavior: 'instant',
      block: 'nearest',
      inline: 'nearest',
    })
    expect(link).toHaveFocus()
  })
})
