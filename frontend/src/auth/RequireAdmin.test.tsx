import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { RequireAdmin } from './RequireAdmin'

function renderRoute(isAdmin: boolean) {
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
    <AuthContext.Provider value={context}>
      <MemoryRouter initialEntries={['/team']}>
        <Routes>
          <Route element={<RequireAdmin />}>
            <Route path="/team" element={<p>Team management</p>} />
          </Route>
          <Route path="/" element={<p>Dashboard</p>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('RequireAdmin', () => {
  it('allows administrators into the protected route', () => {
    renderRoute(true)
    expect(screen.getByText('Team management')).toBeTruthy()
  })

  it('redirects Staff away from the protected route', () => {
    renderRoute(false)
    expect(screen.getByText('Dashboard')).toBeTruthy()
    expect(screen.queryByText('Team management')).toBeNull()
  })
})
