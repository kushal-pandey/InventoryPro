import { Suspense, useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import {
  Boxes,
  ClipboardList,
  LayoutDashboard,
  LogOut,
  Menu,
  PackageSearch,
  Tags,
  Users,
  UserRoundCog,
  Warehouse,
  WifiOff,
} from 'lucide-react'
import { useAuth } from '../auth/useAuth'
import { API_REACHABLE_EVENT, API_UNREACHABLE_EVENT } from '../api/client'
import { initials } from '../lib/format'
import { LoadingBlock } from './States'
import { RouteErrorBoundary } from './RouteErrorBoundary'
import { ThemeToggle } from './ThemeToggle'

const NAV = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/reorder', label: 'Reorder planner', icon: PackageSearch, end: false },
  { to: '/products', label: 'Products', icon: Boxes, end: false },
  { to: '/orders', label: 'Orders', icon: ClipboardList, end: false },
  { to: '/customers', label: 'Customers', icon: Users, end: false },
  { to: '/categories', label: 'Categories', icon: Tags, end: false },
]

export function Layout() {
  const { session, isAdmin, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const [apiUnavailable, setApiUnavailable] = useState(false)
  const [isOffline, setIsOffline] = useState(() => !navigator.onLine)
  const navigation = isAdmin
    ? [...NAV, { to: '/team', label: 'Team', icon: UserRoundCog, end: false }]
    : NAV

  useEffect(() => {
    const offline = () => setIsOffline(true)
    const online = () => setIsOffline(false)
    const apiUnavailable = () => setApiUnavailable(true)
    const apiAvailable = () => setApiUnavailable(false)
    window.addEventListener('offline', offline)
    window.addEventListener('online', online)
    window.addEventListener(API_UNREACHABLE_EVENT, apiUnavailable)
    window.addEventListener(API_REACHABLE_EVENT, apiAvailable)
    return () => {
      window.removeEventListener('offline', offline)
      window.removeEventListener('online', online)
      window.removeEventListener(API_UNREACHABLE_EVENT, apiUnavailable)
      window.removeEventListener(API_REACHABLE_EVENT, apiAvailable)
    }
  }, [])

  return (
    <div className="app-shell">
      <aside className={menuOpen ? 'sidebar open' : 'sidebar'} aria-label="Primary">
        <div className="brand">
          <div className="brand-mark">
            <Warehouse size={22} />
          </div>
          <div>
            <strong>InventoryPro</strong>
            <span>Stock & orders</span>
          </div>
        </div>

        <nav className="nav">
          {navigation.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) => (isActive ? 'nav-item active' : 'nav-item')}
              onClick={() => setMenuOpen(false)}
            >
              <Icon size={19} />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-user">
          <div className="avatar">{initials(session?.fullName ?? '?')}</div>
          <div className="sidebar-user-text">
            <strong>{session?.fullName}</strong>
            <span>{isAdmin ? 'Administrator' : 'Staff'}</span>
          </div>
          <button type="button" className="icon-button on-dark" aria-label="Sign out" onClick={logout}>
            <LogOut size={18} />
          </button>
        </div>
        <ThemeToggle />
      </aside>

      {menuOpen && <button type="button" className="scrim" aria-label="Close menu" onClick={() => setMenuOpen(false)} />}

      <div className="main">
        <header className="topbar">
          <button type="button" className="icon-button menu-toggle" aria-label="Open menu" onClick={() => setMenuOpen(true)}>
            <Menu size={20} />
          </button>
          <span className="topbar-title">InventoryPro</span>
          <ThemeToggle />
        </header>

        <main className="content">
          {(isOffline || apiUnavailable) && (
            <div className="offline-banner" role="status" aria-live="polite">
              <WifiOff size={18} aria-hidden="true" />
              <span>{isOffline ? 'You are offline. Changes will not reach the server.' : 'The API is unreachable. Check that the backend is running.'}</span>
            </div>
          )}
          <Suspense fallback={<LoadingBlock />}>
            <RouteErrorBoundary>
              <Outlet />
            </RouteErrorBoundary>
          </Suspense>
        </main>
      </div>
    </div>
  )
}
