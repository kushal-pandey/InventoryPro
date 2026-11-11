import type { ReactNode } from 'react'

interface MetricCardProps {
  label: string
  value: string
  hint?: string
  tone?: 'default' | 'warning' | 'danger'
  icon: ReactNode
}

export function MetricCard({ label, value, hint, tone = 'default', icon }: MetricCardProps) {
  return (
    <section className={`metric metric-${tone}`}>
      <div className="metric-icon" aria-hidden="true">
        {icon}
      </div>
      <p className="metric-label">{label}</p>
      <p className="metric-value">{value}</p>
      {hint && <p className="metric-hint">{hint}</p>}
    </section>
  )
}
