import { AxiosError, type AxiosResponse } from 'axios'
import { describe, expect, it } from 'vitest'
import { errorMessage } from './errors'

function failure(status: number, data: unknown) {
  return new AxiosError('failed', 'ERR_BAD_REQUEST', undefined, undefined, {
    status,
    data,
  } as AxiosResponse)
}

describe('errorMessage', () => {
  it('explains an unreachable server', () => {
    expect(errorMessage(new AxiosError('Network Error'))).toMatch(/cannot reach the server/i)
  })

  it('joins validation errors from the API', () => {
    const error = failure(400, {
      status: 400,
      message: 'One or more validation errors occurred.',
      errors: { Price: ['Price must be greater than 0.'], Name: ['Name is required.'] },
    })
    expect(errorMessage(error)).toBe('Price must be greater than 0. Name is required.')
  })

  it('uses the message from a standard API error without field errors', () => {
    expect(errorMessage(failure(409, { status: 409, message: 'Conflict.', errors: {} }))).toBe('Conflict.')
  })

  it('prefers the API message (e.g. account lockout)', () => {
    expect(errorMessage(failure(423, { message: 'Account locked. Try again later.' }))).toBe('Account locked. Try again later.')
  })

  it('has sensible fallbacks by status', () => {
    expect(errorMessage(failure(403, undefined))).toMatch(/permission/i)
    expect(errorMessage(failure(429, undefined))).toMatch(/too many requests/i)
    expect(errorMessage(failure(500, undefined))).toMatch(/HTTP 500/)
  })

  it('handles non-HTTP errors', () => {
    expect(errorMessage(new Error('boom'))).toBe('boom')
    expect(errorMessage('weird')).toBe('Something went wrong.')
  })
})
