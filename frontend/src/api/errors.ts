import { isAxiosError } from 'axios'
import type { ApiErrorResponse } from './types'

type ErrorBody = Partial<ApiErrorResponse> & { errors?: Record<string, string[]> | string[] }

/** Turns any thrown value into a message that is safe and useful to show in the UI. */
export function errorMessage(error: unknown): string {
  if (isAxiosError(error)) {
    if (!error.response) {
      return 'Cannot reach the server. Check your connection and that the API is running.'
    }

    const body = error.response.data as ErrorBody | undefined

    if (body?.errors) {
      const details = Array.isArray(body.errors)
        ? body.errors
        : Object.values(body.errors).flat()
      if (details.length > 0) return details.join(' ')
    }
    if (body?.message) return body.message

    if (error.response.status === 403) return 'You do not have permission to do that.'
    if (error.response.status === 429) return 'Too many requests. Please wait a moment and try again.'
    return `The request failed (HTTP ${error.response.status}).`
  }

  return error instanceof Error ? error.message : 'Something went wrong.'
}

export function httpStatus(error: unknown): number | undefined {
  return isAxiosError(error) ? error.response?.status : undefined
}
