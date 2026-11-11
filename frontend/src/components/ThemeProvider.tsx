import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ThemeContext } from './themeContext'

type ThemePreference = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'inventorypro-theme'

function readPreference(): ThemePreference {
  const stored = window.localStorage.getItem(STORAGE_KEY)
  return stored === 'light' || stored === 'dark' ? stored : 'system'
}

function getSystemDark(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreference] = useState<ThemePreference>(() => {
    try {
      return readPreference()
    } catch (error) {
      console.warn('Could not read the saved theme preference.', error)
      return 'system'
    }
  })
  const [systemDark, setSystemDark] = useState(getSystemDark)
  const theme = preference === 'system' ? (systemDark ? 'dark' : 'light') : preference

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return
    const query = window.matchMedia('(prefers-color-scheme: dark)')
    const updateSystemTheme = (event: MediaQueryListEvent) => setSystemDark(event.matches)
    query.addEventListener('change', updateSystemTheme)
    return () => query.removeEventListener('change', updateSystemTheme)
  }, [])

  useEffect(() => {
    document.documentElement.dataset.theme = theme
  }, [theme])

  const toggleTheme = useCallback(() => {
    setPreference(theme === 'dark' ? 'light' : 'dark')
  }, [theme])

  useEffect(() => {
    if (preference === 'system') return
    try {
      window.localStorage.setItem(STORAGE_KEY, preference)
    } catch (error) {
      console.warn('Could not save the theme preference.', error)
    }
  }, [preference])

  const value = useMemo(() => ({ theme, toggleTheme }), [theme, toggleTheme])
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
