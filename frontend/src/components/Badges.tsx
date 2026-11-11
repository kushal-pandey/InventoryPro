import type { OrderStatusValue, StockHealth } from '../api/types'
import { ORDER_STATUS_LABEL } from '../lib/orderStatus'

const ORDER_TONE: Record<OrderStatusValue, string> = {
  0: 'amber',
  1: 'blue',
  2: 'purple',
  3: 'green',
  4: 'red',
}

export function OrderStatusBadge({ status }: { status: OrderStatusValue }) {
  return <span className={`badge badge-${ORDER_TONE[status]}`}>{ORDER_STATUS_LABEL[status]}</span>
}

const HEALTH: Record<StockHealth, { label: string; tone: string }> = {
  OutOfStock: { label: 'Out of stock', tone: 'red' },
  Critical: { label: 'Critical', tone: 'red' },
  ReorderSoon: { label: 'Reorder soon', tone: 'amber' },
  Healthy: { label: 'Healthy', tone: 'green' },
  Overstocked: { label: 'Overstocked', tone: 'blue' },
  Dead: { label: 'Not selling', tone: 'grey' },
}

export function HealthBadge({ health }: { health: StockHealth }) {
  const { label, tone } = HEALTH[health]
  return <span className={`badge badge-${tone}`}>{label}</span>
}

export function ActiveBadge({ active }: { active: boolean }) {
  return <span className={active ? 'badge badge-green' : 'badge badge-grey'}>{active ? 'Active' : 'Inactive'}</span>
}
