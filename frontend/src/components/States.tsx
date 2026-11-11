import type { ReactNode } from 'react'
import { AlertCircle, CheckCircle2, Inbox, RefreshCw } from 'lucide-react'

export function LoadingBlock({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="skeleton-block" role="status" aria-live="polite" aria-busy="true">
      <span className="visually-hidden">{label}</span>
      <span className="skeleton skeleton-title" aria-hidden="true" />
      <span className="skeleton skeleton-line" aria-hidden="true" />
      <span className="skeleton skeleton-line short" aria-hidden="true" />
    </div>
  )
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="state-block">
      <Inbox size={28} aria-hidden="true" />
      <strong>{title}</strong>
      {children && <div className="empty-content">{children}</div>}
    </div>
  )
}

export function ErrorBanner({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="banner banner-error" role="alert">
      <AlertCircle size={18} aria-hidden="true" />
      <span>{message}</span>
      {onRetry && (
        <button type="button" className="btn btn-ghost btn-sm" onClick={onRetry}>
          <RefreshCw size={14} /> Retry
        </button>
      )}
    </div>
  )
}

export function Notice({ message, onDismiss }: { message: string; onDismiss: () => void }) {
  return (
    <div className="banner banner-success" role="status">
      <CheckCircle2 size={18} aria-hidden="true" />
      <span>{message}</span>
      <button type="button" className="btn btn-ghost btn-sm" onClick={onDismiss}>
        Dismiss
      </button>
    </div>
  )
}
