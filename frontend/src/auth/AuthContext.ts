import { createContext } from 'react'
import type { AuthSession, RegisterInput } from '../api/types'

export interface AuthContextValue {
  session: AuthSession | null
  isAdmin: boolean
  login: (email: string, password: string) => Promise<void>
  register: (input: RegisterInput) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
