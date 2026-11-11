import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ThemeProvider } from './ThemeProvider'
import { ThemeToggle } from './ThemeToggle'

describe('theme preference', () => {
  afterEach(() => {
    localStorage.clear()
    delete document.documentElement.dataset.theme
    vi.restoreAllMocks()
  })

  it('follows the system by default and persists a manual theme choice', async () => {
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: vi.fn().mockReturnValue({
        matches: false,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }),
    })

    const view = render(<ThemeProvider><ThemeToggle /></ThemeProvider>)
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('light'))

    fireEvent.click(screen.getByRole('button', { name: 'Switch to dark mode' }))
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('dark'))
    expect(localStorage.getItem('inventorypro-theme')).toBe('dark')

    view.unmount()
    render(<ThemeProvider><ThemeToggle /></ThemeProvider>)
    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('dark'))
  })
})
