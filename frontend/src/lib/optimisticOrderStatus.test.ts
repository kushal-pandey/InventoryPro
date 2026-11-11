import { describe, expect, it } from 'vitest'
import { OrderStatus, type Order } from '../api/types'
import { beginOrderStatusChange, resolveOrderStatusChange } from './optimisticOrderStatus'

const order: Order = {
  id: 8,
  orderNumber: 'ORD-8',
  customerId: 2,
  customerName: 'Customer',
  status: OrderStatus.Pending,
  totalAmount: 20,
  orderDate: '2026-11-09T10:00:00Z',
  items: [],
}

describe('optimistic order status changes', () => {
  it('uses the requested status while the mutation is pending', () => {
    const change = beginOrderStatusChange(order, OrderStatus.Confirmed)
    expect(change.nextStatus).toBe(OrderStatus.Confirmed)
  })

  it('restores the original status on failure and accepts the server value on success', () => {
    const change = beginOrderStatusChange(order, OrderStatus.Cancelled)
    expect(resolveOrderStatusChange(change)).toBe(OrderStatus.Pending)
    expect(resolveOrderStatusChange(change, OrderStatus.Cancelled)).toBe(OrderStatus.Cancelled)
  })
})
