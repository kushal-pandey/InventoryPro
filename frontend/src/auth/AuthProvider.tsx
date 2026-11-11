import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi } from '../api/endpoints'
import type { AuthSession, RegisterInput } from '../api/types'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { clearSession, readSession, UNAUTHORIZED_EVENT, writeSession } from './session'

const MAX_TIMEOUT_MS = 2 ** 31 - 1

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => readSession())

  const login = useCallback(async (email: string, password: string) => {
    const next = await authApi.login(email, password)
    writeSession(next)
    setSession(next)
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    const next = await authApi.register(input)
    writeSession(next)
    setSession(next)
  }, [])

  const logout = useCallback(() => {
    clearSession()
    setSession(null)
  }, [])

  // The API says the token is no longer valid.
  useEffect(() => {
    window.addEventListener(UNAUTHORIZED_EVENT, logout)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, logout)
  }, [logout])

  // Sign out automatically the moment the token expires.
  useEffect(() => {
    if (!session) return
    const remaining = new Date(session.expiresAt).getTime() - Date.now()
    const timer = setTimeout(logout, Math.min(Math.max(remaining, 0), MAX_TIMEOUT_MS))
    return () => clearTimeout(timer)
  }, [session, logout])

  const value = useMemo<AuthContextValue>(
    () => ({ session, isAdmin: session?.roles.includes('Admin') ?? false, login, register, logout }),
    [session, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
