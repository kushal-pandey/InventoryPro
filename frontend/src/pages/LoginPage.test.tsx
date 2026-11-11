import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { authApi } from '../api/endpoints'
import { AuthContext, type AuthContextValue } from '../auth/AuthContext'
import { ThemeProvider } from '../components/ThemeProvider'
import LoginPage from './LoginPage'

vi.mock('../api/endpoints', () => ({
  authApi: {
    registrationStatus: vi.fn(),
    login: vi.fn(),
    register: vi.fn(),
  },
}))

const authValue: AuthContextValue = {
  session: null,
  isAdmin: false,
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

function renderLogin() {
  return render(
    <ThemeProvider>
      <AuthContext.Provider value={authValue}>
        <MemoryRouter initialEntries={['/login']}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<p>Signed in</p>} />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>
    </ThemeProvider>,
  )
}

describe('LoginPage registration', () => {
  beforeEach(() => {
    vi.mocked(authApi.registrationStatus).mockResolvedValue({ enabled: true })
    vi.mocked(authValue.register).mockResolvedValue(undefined)
  })

  it('only offers account creation when the API enables registration', async () => {
    vi.mocked(authApi.registrationStatus).mockResolvedValue({ enabled: false })
    renderLogin()

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /create an account/i })).toBeNull()
  })

  it('creates a Staff account with a compliant password and signs in', async () => {
    renderLogin()
    fireEvent.click(await screen.findByRole('button', { name: 'New here? Create an account' }))

    expect(screen.getByText('New accounts are created as Staff. Staff members can view customer and order data.')).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New Staff Member' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'staff@example.test' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'StrongPass!2026' } })
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'StrongPass!2026' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(authValue.register).toHaveBeenCalledWith({
      fullName: 'New Staff Member',
      email: 'staff@example.test',
      password: 'StrongPass!2026',
    }))
    expect(await screen.findByText('Signed in')).toBeTruthy()
  })
})
