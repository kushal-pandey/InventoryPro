import { describe, expect, it } from 'vitest'
import { OrderStatus } from '../api/types'
import { NEXT_STATUSES } from './orderStatus'

describe('order status transitions (must mirror the API)', () => {
  it('moves forward only', () => {
    expect(NEXT_STATUSES[OrderStatus.Pending]).toEqual([OrderStatus.Confirmed, OrderStatus.Cancelled])
    expect(NEXT_STATUSES[OrderStatus.Confirmed]).toEqual([OrderStatus.Shipped, OrderStatus.Cancelled])
    expect(NEXT_STATUSES[OrderStatus.Shipped]).toEqual([OrderStatus.Delivered])
  })

  it('treats Delivered and Cancelled as final', () => {
    expect(NEXT_STATUSES[OrderStatus.Delivered]).toEqual([])
    expect(NEXT_STATUSES[OrderStatus.Cancelled]).toEqual([])
  })

  it('does not allow cancelling once shipped', () => {
    expect(NEXT_STATUSES[OrderStatus.Shipped]).not.toContain(OrderStatus.Cancelled)
  })
})
