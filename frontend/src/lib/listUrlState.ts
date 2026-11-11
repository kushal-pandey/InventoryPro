export type ProductSort = 'name:asc' | 'name:desc' | 'price:asc' | 'price:desc' | 'stock:asc' | 'stock:desc' | 'createdAt:desc'
export type OrderSort = 'orderDate:asc' | 'orderDate:desc' | 'orderNumber:asc' | 'orderNumber:desc' | 'customer:asc' | 'customer:desc' | 'total:asc' | 'total:desc'

export interface ProductListUrlState {
  page: number
  search: string
  categoryId: number
  sort: ProductSort
  lowStockOnly: boolean
}

export interface OrderListUrlState {
  page: number
  search: string
  status: string
  sort: OrderSort
}

function positiveInt(value: string | null, fallback: number): number {
  if (!value) return fallback
  const parsed = Number(value)
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback
}

export function readProductListUrl(params: URLSearchParams): ProductListUrlState {
  const sort = params.get('sort')
  const allowedSorts: ProductSort[] = ['name:asc', 'name:desc', 'price:asc', 'price:desc', 'stock:asc', 'stock:desc', 'createdAt:desc']
  return {
    page: positiveInt(params.get('page'), 1),
    search: params.get('q') ?? '',
    categoryId: positiveInt(params.get('category'), 0),
    sort: allowedSorts.includes(sort as ProductSort) ? (sort as ProductSort) : 'name:asc',
    lowStockOnly: params.get('low') === 'true',
  }
}

export function readOrderListUrl(params: URLSearchParams): OrderListUrlState {
  const sort = params.get('sort')
  const allowedSorts: OrderSort[] = [
    'orderDate:asc', 'orderDate:desc', 'orderNumber:asc', 'orderNumber:desc',
    'customer:asc', 'customer:desc', 'total:asc', 'total:desc',
  ]
  const status = params.get('status') ?? ''
  return {
    page: positiveInt(params.get('page'), 1),
    search: params.get('q') ?? '',
    status: ['0', '1', '2', '3', '4'].includes(status) ? status : '',
    sort: allowedSorts.includes(sort as OrderSort) ? (sort as OrderSort) : 'orderDate:desc',
  }
}

export function updateListUrl(
  current: URLSearchParams,
  updates: Record<string, string | number | boolean | null | undefined>,
  resetPage = false,
): URLSearchParams {
  const next = new URLSearchParams(current)
  if (resetPage) next.delete('page')
  for (const [key, value] of Object.entries(updates)) {
    if (value === null || value === undefined || value === '') next.delete(key)
    else next.set(key, String(value))
  }
  return next
}
