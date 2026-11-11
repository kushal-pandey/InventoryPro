import { OrderStatus, type OrderStatusValue } from '../api/types'

export const ORDER_STATUS_LABEL: Record<OrderStatusValue, string> = {
  [OrderStatus.Pending]: 'Pending',
  [OrderStatus.Confirmed]: 'Confirmed',
  [OrderStatus.Shipped]: 'Shipped',
  [OrderStatus.Delivered]: 'Delivered',
  [OrderStatus.Cancelled]: 'Cancelled',
}

/** Mirrors the transition rules enforced by the API (the API remains the source of truth). */
export const NEXT_STATUSES: Record<OrderStatusValue, OrderStatusValue[]> = {
  [OrderStatus.Pending]: [OrderStatus.Confirmed, OrderStatus.Cancelled],
  [OrderStatus.Confirmed]: [OrderStatus.Shipped, OrderStatus.Cancelled],
  [OrderStatus.Shipped]: [OrderStatus.Delivered],
  [OrderStatus.Delivered]: [],
  [OrderStatus.Cancelled]: [],
}
