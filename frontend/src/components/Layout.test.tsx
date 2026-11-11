import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../auth/AuthContext'
import { Layout } from './Layout'
import { ThemeProvider } from './ThemeProvider'

function renderLayout(isAdmin: boolean) {
  const context: AuthContextValue = {
    session: {
      token: 'token',
      expiresAt: '2099-01-01T00:00:00Z',
      email: 'user@example.test',
      fullName: 'Test User',
      roles: isAdmin ? ['Admin'] : ['Staff'],
    },
    isAdmin,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  }
  return render(
    <ThemeProvider>
      <AuthContext.Provider value={context}>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<p>Dashboard</p>} />
              <Route path="team" element={<p>Team</p>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>
    </ThemeProvider>,
  )
}

describe('Layout navigation', () => {
  it('shows Team navigation for administrators', () => {
    renderLayout(true)
    expect(screen.getByRole('link', { name: 'Team' })).toBeTruthy()
  })

  it('hides Team navigation for Staff', () => {
    renderLayout(false)
    expect(screen.queryByRole('link', { name: 'Team' })).toBeNull()
  })
})
