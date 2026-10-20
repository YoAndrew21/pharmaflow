import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { Skeleton } from './Skeleton'
import { MedicationCardSkeleton } from './MedicationCardSkeleton'
import { ReadRetryButton } from './ReadRetryButton'

it('renders only hidden, empty, non-interactive skeleton geometry', async () => {
  const view = render(
    <>
      <Skeleton />
      <MedicationCardSkeleton />
      <button>Next</button>
    </>,
  )
  const skeletons = view.container.querySelectorAll('[data-skeleton]')
  expect(skeletons.length).toBeGreaterThan(0)
  for (const skeleton of skeletons) {
    expect(skeleton).toHaveAttribute('aria-hidden', 'true')
    expect(skeleton.textContent).toBe('')
    expect(
      skeleton.querySelector(
        'a, button, input, select, textarea, [tabindex], [role], time',
      ),
    ).toBeNull()
  }
  expect(screen.queryByRole('article')).not.toBeInTheDocument()
  await userEvent.tab()
  expect(screen.getByRole('button', { name: 'Next' })).toHaveFocus()
  view.unmount()
  for (const skeleton of skeletons) expect(skeleton).not.toBeInTheDocument()
})

it('keeps Retry focused during the pending operation and does not resurrect it on a later ordinary read', async () => {
  const user = userEvent.setup()
  const onRetry = vi.fn()
  const view = render(
    <ReadRetryButton isError isLoading={false} onRetry={onRetry} />,
  )
  const button = screen.getByRole('button', { name: 'Try again' })
  await user.click(button)
  expect(onRetry).toHaveBeenCalledOnce()
  view.rerender(<ReadRetryButton isError={false} isLoading onRetry={onRetry} />)
  expect(screen.getByRole('button', { name: 'Try again' })).toBe(button)
  expect(button).toHaveFocus()
  expect(button).toHaveAttribute('aria-disabled', 'true')
  await user.keyboard('{Enter}')
  expect(onRetry).toHaveBeenCalledOnce()
  view.rerender(
    <ReadRetryButton isError={false} isLoading={false} onRetry={onRetry} />,
  )
  expect(button).not.toBeInTheDocument()
  view.rerender(<ReadRetryButton isError={false} isLoading onRetry={onRetry} />)
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
})
