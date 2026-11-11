import type { ReorderSuggestion } from '../api/types'

export function toggleReorderSelection(selectedIds: readonly number[], productId: number): number[] {
  return selectedIds.includes(productId)
    ? selectedIds.filter((id) => id !== productId)
    : [...selectedIds, productId]
}

export function selectAllNeedingReorder(
  items: readonly ReorderSuggestion[],
  selectedIds: readonly number[],
): number[] {
  const reorderIds = items.filter((item) => item.suggestedOrderQuantity > 0).map((item) => item.productId)
  const allSelected = reorderIds.length > 0 && reorderIds.every((id) => selectedIds.includes(id))
  return allSelected ? selectedIds.filter((id) => !reorderIds.includes(id)) : [...new Set([...selectedIds, ...reorderIds])]
}

export function buildSelectedPurchaseList(
  items: readonly ReorderSuggestion[],
  selectedIds: readonly number[],
  quantities: Readonly<Record<number, string>>,
): ReorderSuggestion[] {
  const selected = new Set(selectedIds)
  return items
    .filter((item) => selected.has(item.productId) && item.suggestedOrderQuantity > 0)
    .map((item) => {
      const entered = Number.parseInt(quantities[item.productId] ?? '', 10)
      const orderQuantity = Number.isSafeInteger(entered) && entered > 0 ? entered : item.suggestedOrderQuantity
      return { ...item, suggestedOrderQuantity: orderQuantity }
    })
}
