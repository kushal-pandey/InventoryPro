import type { Order, OrderStatusValue } from '../api/types'

export interface OrderStatusChange {
  orderId: number
  previousStatus: OrderStatusValue
  nextStatus: OrderStatusValue
}

export function beginOrderStatusChange(order: Order, nextStatus: OrderStatusValue): OrderStatusChange {
  return { orderId: order.id, previousStatus: order.status, nextStatus }
}

export function resolveOrderStatusChange(change: OrderStatusChange, savedStatus?: OrderStatusValue): OrderStatusValue {
  return savedStatus ?? change.previousStatus
}
