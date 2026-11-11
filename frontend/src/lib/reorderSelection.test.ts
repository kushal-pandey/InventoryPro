import { describe, expect, it } from 'vitest'
import type { ReorderSuggestion } from '../api/types'
import { buildSelectedPurchaseList, selectAllNeedingReorder, toggleReorderSelection } from './reorderSelection'

const items: ReorderSuggestion[] = [
  { productId: 1, name: 'Tea', sku: 'TEA', categoryName: 'Drinks', price: 4, stockOnHand: 2, reorderLevel: 5, unitsSold: 20, windowDays: 30, dailyDemand: 0.67, daysOfCover: 3, reorderPoint: 7, suggestedOrderQuantity: 8, health: 'Critical' },
  { productId: 2, name: 'Rice', sku: 'RICE', categoryName: 'Food', price: 8, stockOnHand: 30, reorderLevel: 5, unitsSold: 0, windowDays: 30, dailyDemand: 0, daysOfCover: null, reorderPoint: 5, suggestedOrderQuantity: 0, health: 'Healthy' },
]

describe('reorder selection', () => {
  it('toggles row selection and selects only products needing reorder', () => {
    expect(toggleReorderSelection([], 1)).toEqual([1])
    expect(toggleReorderSelection([1], 1)).toEqual([])
    expect(selectAllNeedingReorder(items, [])).toEqual([1])
    expect(selectAllNeedingReorder(items, [1])).toEqual([])
  })

  it('exports selected products with their edited positive quantities', () => {
    const selected = buildSelectedPurchaseList(items, [1, 2], { 1: '12', 2: '99' })
    expect(selected).toHaveLength(1)
    expect(selected[0]?.suggestedOrderQuantity).toBe(12)
    expect(buildSelectedPurchaseList(items, [1], { 1: '0' })[0]?.suggestedOrderQuantity).toBe(8)
  })
})
