/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { ThemeToggle } from './ThemeToggle'
import { THEME_KEY } from '../features/preferences/theme'

beforeEach(() => {
  localStorage.removeItem(THEME_KEY)
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: false })),
  )
})
afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  localStorage.removeItem(THEME_KEY)
})

it('keeps the native theme action named, keyboard operable, and persisted', async () => {
  render(<ThemeToggle />)
  const toggle = screen.getByRole('button', { name: 'Switch to dark theme' })
  expect(toggle).toHaveAttribute('type', 'button')
  await userEvent.tab()
  expect(toggle).toHaveFocus()
  await userEvent.keyboard(' ')
  expect(document.documentElement.dataset.theme).toBe('dark')
  expect(document.documentElement.style.colorScheme).toBe('dark')
  expect(localStorage.getItem(THEME_KEY)).toBe('dark')
  expect(toggle).toHaveAccessibleName('Switch to light theme')
  await userEvent.keyboard('{Enter}')
  expect(document.documentElement.dataset.theme).toBe('light')
  expect(localStorage.getItem(THEME_KEY)).toBe('light')
  expect(toggle).toHaveAccessibleName('Switch to dark theme')
})

it('preserves a stored theme over the initial system preference', () => {
  localStorage.setItem(THEME_KEY, 'dark')
  render(<ThemeToggle />)
  expect(
    screen.getByRole('button', { name: 'Switch to light theme' }),
  ).toBeInTheDocument()
  expect(document.documentElement.dataset.theme).toBe('dark')
})

it('preserves the system fallback and memory-only toggling when storage is blocked', async () => {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: true })),
  )
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
    throw new Error('Storage blocked')
  })
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('Storage blocked')
  })
  render(<ThemeToggle />)
  await userEvent.click(
    screen.getByRole('button', { name: 'Switch to light theme' }),
  )
  expect(document.documentElement.dataset.theme).toBe('light')
  expect(
    screen.getByRole('button', { name: 'Switch to dark theme' }),
  ).toBeInTheDocument()
})

it('keeps header layout CSS from hiding or restyling unrelated nested control content', () => {
  // jsdom does not evaluate responsive CSS. Guard the cross-component selector
  // that would otherwise hide a future language trigger's selected-value span.
  const headerCss = readFileSync('src/layouts/AppLayout.module.css', 'utf8')
  expect(headerCss).not.toMatch(/\.theme\s+(?:button|label|span)\b/)
})
