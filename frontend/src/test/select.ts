import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

export function selectTrigger(label: string) {
  // React Aria names the button with both the label and selected text.
  return screen.getByRole('button', { name: (name) => name.includes(label) })
}

export async function chooseOption(trigger: HTMLElement, label: string) {
  await userEvent.click(trigger)
  await userEvent.click(
    within(screen.getByRole('listbox')).getByRole('option', { name: label }),
  )
}
