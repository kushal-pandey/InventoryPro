import axios from 'axios'
import { readSession, UNAUTHORIZED_EVENT } from '../auth/session'

const baseURL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080/api'

export const api = axios.create({ baseURL, timeout: 15_000 })
export const API_UNREACHABLE_EVENT = 'inventorypro:api-unreachable'
export const API_REACHABLE_EVENT = 'inventorypro:api-reachable'

api.interceptors.request.use((config) => {
  const session = readSession()
  if (session) config.headers.Authorization = `Bearer ${session.token}`
  return config
})

api.interceptors.response.use(
  (response) => {
    window.dispatchEvent(new Event(API_REACHABLE_EVENT))
    return response
  },
  (error: unknown) => {
    if (axios.isAxiosError(error) && !error.response) {
      window.dispatchEvent(new Event(API_UNREACHABLE_EVENT))
    }
    // A 401 while holding a session means the token expired or was revoked: sign out everywhere.
    // (A failed login has no session yet, so it is not affected.)
    if (axios.isAxiosError(error) && error.response?.status === 401 && readSession()) {
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    }
    return Promise.reject(error)
  },
)
