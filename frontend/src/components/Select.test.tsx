import { useState } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import userEvent from '@testing-library/user-event'
import { Select } from './Select'

const options = [
  { value: '', label: 'All branches' },
  { value: 'aster-commons', label: 'Aster Commons' },
  { value: 'willow-arcade', label: 'Willow Arcade' },
  { value: 'lantern-court', label: 'Lantern Court' },
]

function Controlled({
  onChange = vi.fn(),
}: {
  onChange?: (value: string) => void
}) {
  const [value, setValue] = useState('')
  return (
    <form>
      <button type="button">Before</button>
      <Select
        name="branch"
        label="Branch"
        value={value}
        options={options}
        onChange={(next) => {
          setValue(next)
          onChange(next)
        }}
      />
      <button type="button">After</button>
    </form>
  )
}

it('treats the empty string as a selected All option and preserves the named form value', async () => {
  const view = render(
    <form>
      <Select
        name="branch"
        label="Branch"
        value=""
        onChange={vi.fn()}
        options={[
          { value: '', label: 'All branches' },
          { value: 'aster-commons', label: 'Aster Commons' },
        ]}
      />
    </form>,
  )
  const trigger = screen.getByRole('button', { name: /Branch/ })
  expect(trigger).toHaveTextContent('All branches')
  expect(
    new FormData(view.container.querySelector('form')!).get('branch'),
  ).toBe('')
  await userEvent.click(trigger)
  expect(screen.getByRole('option', { name: 'All branches' })).toHaveAttribute(
    'aria-selected',
    'true',
  )
})

it('commits pointer choices once, restores focus, and submits the original named values including empty', async () => {
  const user = userEvent.setup()
  const change = vi.fn()
  const view = render(<Controlled onChange={change} />)
  const trigger = screen.getByRole('button', { name: /Branch/ })
  await user.click(trigger)
  expect(trigger).toHaveAttribute('aria-expanded', 'true')
  await user.click(screen.getByRole('option', { name: 'Willow Arcade' }))
  expect(change).toHaveBeenCalledExactlyOnceWith('willow-arcade')
  expect(trigger).toHaveTextContent('Willow Arcade')
  await waitFor(() => expect(trigger).toHaveFocus())
  expect(trigger).toHaveAttribute('aria-expanded', 'false')
  expect(
    new FormData(view.container.querySelector('form')!).get('branch'),
  ).toBe('willow-arcade')
  await user.click(trigger)
  expect(screen.getByRole('option', { name: 'Willow Arcade' })).toHaveAttribute(
    'aria-selected',
    'true',
  )
  await user.click(screen.getByRole('option', { name: 'All branches' }))
  expect(change).toHaveBeenLastCalledWith('')
  expect(
    new FormData(view.container.querySelector('form')!).get('branch'),
  ).toBe('')
})

it.each(['{Enter}', ' '])(
  'opens and commits with %s while supporting arrows/Home/End and tab navigation',
  async (key) => {
    const user = userEvent.setup()
    const change = vi.fn()
    render(<Controlled onChange={change} />)
    await user.tab()
    expect(screen.getByRole('button', { name: 'Before' })).toHaveFocus()
    await user.tab()
    const trigger = screen.getByRole('button', { name: /Branch/ })
    await waitFor(() => expect(trigger).toHaveFocus())
    expect(trigger).toHaveAttribute('data-focus-visible')
    await user.keyboard(key)
    expect(trigger).toHaveAttribute('aria-expanded', 'true')
    await user.keyboard('{End}')
    expect(screen.getByRole('option', { name: 'Lantern Court' })).toHaveFocus()
    await user.keyboard('{Home}')
    expect(screen.getByRole('option', { name: 'All branches' })).toHaveFocus()
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowUp}')
    expect(screen.getByRole('option', { name: 'Aster Commons' })).toHaveFocus()
    expect(change).not.toHaveBeenCalled()
    await user.keyboard(key === ' ' ? ' ' : '{Enter}')
    expect(change).toHaveBeenCalledExactlyOnceWith('aster-commons')
    expect(trigger).toHaveTextContent('Aster Commons')
    await waitFor(() => expect(trigger).toHaveFocus())
    await user.tab()
    expect(screen.getByRole('button', { name: 'After' })).toHaveFocus()
    await user.tab({ shift: true })
    await waitFor(() => expect(trigger).toHaveFocus())
  },
)

it.each(['{ArrowDown}', '{ArrowUp}'])(
  'opens with %s and cancels highlight changes with Escape',
  async (key) => {
    const user = userEvent.setup()
    const change = vi.fn()
    render(<Controlled onChange={change} />)
    const trigger = screen.getByRole('button', { name: /Branch/ })
    trigger.focus()
    await user.keyboard(key)
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    await user.keyboard('{End}{Escape}')
    expect(change).not.toHaveBeenCalled()
    expect(trigger).toHaveTextContent('All branches')
    await waitFor(() => expect(trigger).toHaveFocus())
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  },
)

it('uses typeahead on the open list and Enter to commit', async () => {
  const user = userEvent.setup()
  const change = vi.fn()
  render(<Controlled onChange={change} />)
  const trigger = screen.getByRole('button', { name: /Branch/ })
  trigger.focus()
  await user.keyboard('{Enter}will')
  expect(screen.getByRole('option', { name: 'Willow Arcade' })).toHaveFocus()
  await user.keyboard('{Enter}')
  expect(change).toHaveBeenCalledExactlyOnceWith('willow-arcade')
  await waitFor(() => expect(trigger).toHaveFocus())
})

it('dismisses outside the portaled list without committing and cleans up on unmount', async () => {
  const user = userEvent.setup()
  const change = vi.fn()
  const view = render(<Controlled onChange={change} />)
  const trigger = screen.getByRole('button', { name: /Branch/ })
  await user.click(trigger)
  const list = screen.getByRole('listbox')
  expect(view.container).not.toContainElement(list)
  await user.click(document.body)
  await waitFor(() => expect(list).not.toBeInTheDocument())
  expect(change).not.toHaveBeenCalled()
  await user.click(trigger)
  const reopenedList = screen.getByRole('listbox')
  expect(reopenedList).toBeInTheDocument()
  view.unmount()
  expect(reopenedList).not.toBeInTheDocument()
})

it('keeps disabled controls out of the tab sequence and prevents opening', async () => {
  const user = userEvent.setup()
  const change = vi.fn()
  render(
    <>
      <Select
        label="Branch"
        value=""
        options={options}
        onChange={change}
        disabled
      />
      <button>Next</button>
    </>,
  )
  const trigger = screen.getByRole('button', { name: /Branch/ })
  expect(trigger).toBeDisabled()
  await user.click(trigger)
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  await user.tab()
  expect(screen.getByRole('button', { name: 'Next' })).toHaveFocus()
  expect(change).not.toHaveBeenCalled()
})

it('preserves authored language metadata in the selected value and options', async () => {
  render(
    <Select
      label="Language"
      value="ja"
      onChange={vi.fn()}
      options={[{ value: 'ja', label: '日本語', lang: 'ja' }]}
      variant="compact"
    />,
  )
  const trigger = screen.getByRole('button', { name: /Language/ })
  expect(trigger.querySelector('[lang="ja"]')).toHaveTextContent('日本語')
  await userEvent.click(trigger)
  expect(
    screen.getByRole('option', { name: '日本語' }).querySelector('[lang="ja"]'),
  ).toHaveTextContent('日本語')
})
