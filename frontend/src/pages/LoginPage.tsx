import { useCallback, useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Check, Warehouse } from 'lucide-react'
import { authApi } from '../api/endpoints'
import { errorMessage } from '../api/errors'
import { useAuth } from '../auth/useAuth'
import { ThemeToggle } from '../components/ThemeToggle'
import { ErrorBanner } from '../components/States'
import { useQuery } from '../hooks/useQuery'
import { getPasswordRules } from '../lib/passwordRules'

type AuthMode = 'login' | 'register'

export default function LoginPage() {
  const { session, login, register } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'
  const [mode, setMode] = useState<AuthMode>('login')
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const registration = useQuery(useCallback(() => authApi.registrationStatus(), []))
  const passwordRules = getPasswordRules(password)
  const passwordValid = passwordRules.every((rule) => rule.valid)

  if (session) return <Navigate to={from} replace />

  function changeMode(nextMode: AuthMode) {
    setMode(nextMode)
    setError(null)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    if (mode === 'register' && password !== confirmPassword) {
      setError('Passwords do not match.')
      setBusy(false)
      return
    }

    try {
      if (mode === 'register') {
        await register({ fullName: fullName.trim(), email: email.trim(), password })
      } else {
        await login(email.trim(), password)
      }
      navigate(from, { replace: true })
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-theme"><ThemeToggle /></div>
      <form className="login-card" onSubmit={submit}>
        <div className="brand brand-light">
          <div className="brand-mark">
            <Warehouse size={22} />
          </div>
          <div>
            <strong>InventoryPro</strong>
            <span>Know what to reorder, before you run out</span>
          </div>
        </div>

        <h1>{mode === 'register' ? 'Create your account' : 'Sign in'}</h1>

        {registration.error && (
          <ErrorBanner message={`Could not check whether registration is enabled: ${registration.error}`} onRetry={registration.reload} />
        )}

        {mode === 'register' && (
          <div className="field">
            <label htmlFor="full-name">Full name</label>
            <input
              id="full-name"
              type="text"
              autoComplete="name"
              required
              maxLength={100}
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
            />
          </div>
        )}

        <div className="field">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
            required
            aria-describedby={mode === 'register' ? 'register-password-rules' : undefined}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </div>

        {mode === 'register' && (
          <>
            <ul className="password-rules" id="register-password-rules" aria-label="Password requirements" aria-live="polite">
              {passwordRules.map((rule) => (
                <li key={rule.id} className={rule.valid ? 'password-rule valid' : 'password-rule'}>
                  <Check size={14} aria-hidden="true" />
                  {rule.label}
                </li>
              ))}
            </ul>

            <div className="field">
              <label htmlFor="confirm-password">Confirm password</label>
              <input
                id="confirm-password"
                type="password"
                autoComplete="new-password"
                required
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
              />
            </div>

            <p className="registration-note">
              New accounts are created as Staff. Staff members can view customer and order data.
            </p>
          </>
        )}

        {error && <p className="form-error" role="alert">{error}</p>}

        <button
          type="submit"
          className="btn btn-primary btn-block"
          disabled={busy || (mode === 'register' && (!passwordValid || !registration.data?.enabled))}
        >
          {busy ? 'Please wait…' : mode === 'register' ? 'Create account' : 'Sign in'}
        </button>

        {registration.data?.enabled && (
          <button
            type="button"
            className="auth-mode-toggle"
            onClick={() => changeMode(mode === 'register' ? 'login' : 'register')}
            disabled={busy}
          >
            {mode === 'register' ? 'Already have an account? Sign in' : 'New here? Create an account'}
          </button>
        )}
      </form>
    </div>
  )
}
