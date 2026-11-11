import type { AuthSession } from '../api/types'

// NOTE: a JWT in web storage is readable by any script on the page (XSS). It is acceptable for
// a portfolio/demo; for production, move the token into an httpOnly, SameSite cookie issued by the API.
const STORAGE_KEY = 'inventorypro.session'

export const UNAUTHORIZED_EVENT = 'inventorypro:unauthorized'

export function readSession(): AuthSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null

    const session = JSON.parse(raw) as AuthSession
    if (!session.token || !session.expiresAt || new Date(session.expiresAt).getTime() <= Date.now()) {
      localStorage.removeItem(STORAGE_KEY)
      return null
    }
    return session
  } catch {
    localStorage.removeItem(STORAGE_KEY)
    return null
  }
}

export function writeSession(session: AuthSession): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}

export function clearSession(): void {
  localStorage.removeItem(STORAGE_KEY)
}
