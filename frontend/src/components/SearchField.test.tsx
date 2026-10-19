import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { SearchField } from './SearchField'

it('shows the clear action for any content including spaces and removes it when empty', async () => {
  const user = userEvent.setup()
  render(<SearchField />)
  const input = screen.getByRole('searchbox', {
    name: 'Medication name or active ingredient',
  })
  expect(
    screen.queryByRole('button', { name: 'Clear search text' }),
  ).not.toBeInTheDocument()
  await user.type(input, '   ')
  expect(
    screen.getByRole('button', { name: 'Clear search text' }),
  ).toHaveAttribute('type', 'button')
  await user.clear(input)
  expect(
    screen.queryByRole('button', { name: 'Clear search text' }),
  ).not.toBeInTheDocument()
})

it.each(['pointer', 'Enter', 'Space'] as const)(
  'clears with %s, restores input focus, and leaves submitting to the form',
  async (method) => {
    const user = userEvent.setup()
    const submitted = vi.fn()
    render(
      <form
        onSubmit={(event) => {
          event.preventDefault()
          submitted(Object.fromEntries(new FormData(event.currentTarget)))
        }}
      >
        <SearchField defaultValue="para" />
        <input type="hidden" name="branch" value="aster-commons" />
        <button type="submit">Send</button>
      </form>,
    )
    const input = screen.getByRole('searchbox')
    const clear = screen.getByRole('button', { name: 'Clear search text' })
    if (method === 'pointer') await user.click(clear)
    else {
      await user.tab()
      expect(input).toHaveFocus()
      await user.tab()
      expect(clear).toHaveFocus()
      await user.keyboard(method === 'Enter' ? '{Enter}' : ' ')
    }
    expect(input).toHaveValue('')
    expect(input).toHaveFocus()
    expect(submitted).not.toHaveBeenCalled()
    expect(new FormData(input.closest('form')!).get('q')).toBe('')
    expect(
      screen.queryByRole('button', { name: 'Clear search text' }),
    ).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Send' }))
    expect(submitted).toHaveBeenCalledExactlyOnceWith({
      q: '',
      branch: 'aster-commons',
    })
  },
)

it('adopts changed defaults without remounting and keeps the draft when the default is unchanged', async () => {
  const user = userEvent.setup()
  const view = render(<SearchField defaultValue="para" />)
  const input = screen.getByRole('searchbox')
  await user.type(input, ' draft')
  view.rerender(<SearchField defaultValue="para" />)
  expect(input).toHaveValue('para draft')
  expect(input).toHaveFocus()
  view.rerender(<SearchField defaultValue="caffeine" />)
  expect(screen.getByRole('searchbox')).toBe(input)
  expect(input).toHaveValue('caffeine')
  await user.click(screen.getByRole('button', { name: 'Clear search text' }))
  view.rerender(<SearchField defaultValue="caffeine" />)
  expect(input).toHaveValue('')
  view.rerender(<SearchField defaultValue="para" />)
  expect(input).toHaveValue('para')
  view.rerender(<SearchField defaultValue="" />)
  expect(input).toHaveValue('')
  expect(
    screen.queryByRole('button', { name: 'Clear search text' }),
  ).not.toBeInTheDocument()
})

it.each(['', 'para'])(
  'honors native form reset with default %j, including resets without a prop change',
  async (defaultValue) => {
    const user = userEvent.setup()
    render(
      <form>
        <SearchField defaultValue={defaultValue} />
        <button type="reset">Reset</button>
      </form>,
    )
    const input = screen.getByRole('searchbox')
    await user.type(input, ' draft')
    await user.click(screen.getByRole('button', { name: 'Reset' }))
    expect(input).toHaveValue(defaultValue)
    expect(new FormData(input.closest('form')!).get('q')).toBe(defaultValue)
    if (defaultValue)
      expect(
        screen.getByRole('button', { name: 'Clear search text' }),
      ).toBeInTheDocument()
    else
      expect(
        screen.queryByRole('button', { name: 'Clear search text' }),
      ).not.toBeInTheDocument()
  },
)

it('uses the latest default in its reset subscription', async () => {
  const user = userEvent.setup()
  const form = (value: string) => (
    <form>
      <SearchField defaultValue={value} />
      <button type="reset">Reset</button>
    </form>
  )
  const view = render(form('para'))
  view.rerender(form('caffeine'))
  await user.click(screen.getByRole('button', { name: 'Clear search text' }))
  await user.click(screen.getByRole('button', { name: 'Reset' }))
  expect(screen.getByRole('searchbox')).toHaveValue('caffeine')
})
