import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useQuery } from './useQuery'

describe('useQuery', () => {
  it('loads data and exposes it', async () => {
    const fetcher = vi.fn().mockResolvedValue(['a', 'b'])
    const { result } = renderHook(() => useQuery(fetcher))

    expect(result.current.loading).toBe(true)
    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current.data).toEqual(['a', 'b'])
    expect(result.current.error).toBeUndefined()
    expect(fetcher).toHaveBeenCalledTimes(1)
  })

  it('surfaces a readable error', async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error('nope'))
    const { result } = renderHook(() => useQuery(fetcher))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current.error).toBe('nope')
    expect(result.current.data).toBeUndefined()
  })

  it('refetches on reload()', async () => {
    const fetcher = vi.fn().mockResolvedValue(1)
    const { result } = renderHook(() => useQuery(fetcher))
    await waitFor(() => expect(result.current.loading).toBe(false))

    act(() => result.current.reload())
    await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2))
  })

  it('ignores a slow response that was superseded by a newer request', async () => {
    let resolveSlow: (value: string) => void = () => undefined
    const slow = vi.fn(() => new Promise<string>((resolve) => { resolveSlow = resolve }))
    const fast = vi.fn().mockResolvedValue('fast')

    const { result, rerender } = renderHook(({ fetcher }) => useQuery(fetcher), { initialProps: { fetcher: slow } })
    rerender({ fetcher: fast })
    await waitFor(() => expect(result.current.data).toBe('fast'))

    await act(async () => resolveSlow('slow'))
    expect(result.current.data).toBe('fast')
  })
})
