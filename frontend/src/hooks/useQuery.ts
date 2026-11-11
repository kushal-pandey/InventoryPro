import { useCallback, useEffect, useState } from 'react'
import { errorMessage } from '../api/errors'

interface Result<T> {
  source: unknown
  tick: number
  updatedAt: number
  data?: T
  error?: string
}

/**
 * Minimal data-fetching hook. Pass a MEMOISED fetcher (useCallback); a new fetcher identity starts
 * a new request. Previous data stays visible while the next request is in flight, and results from
 * superseded requests are ignored.
 */
export function useQuery<T>(fetcher: () => Promise<T>) {
  const [tick, setTick] = useState(0)
  const [result, setResult] = useState<Result<T> | null>(null)

  useEffect(() => {
    let active = true

    fetcher()
      .then((data) => {
        if (active) setResult({ source: fetcher, tick, updatedAt: Date.now(), data })
      })
      .catch((error: unknown) => {
        if (active) setResult({ source: fetcher, tick, updatedAt: Date.now(), error: errorMessage(error) })
      })

    return () => {
      active = false
    }
  }, [fetcher, tick])

  const loading = result === null || result.source !== fetcher || result.tick !== tick

  const reload = useCallback(() => setTick((value) => value + 1), [])

  return {
    data: result?.data,
    error: loading ? undefined : result?.error,
    loading,
    reload,
    updatedAt: loading ? undefined : result?.updatedAt,
  }
}
