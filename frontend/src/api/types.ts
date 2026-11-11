// Mirrors the backend DTOs (camelCase JSON). Enums are numeric on the wire.

export const OrderStatus = {
  Pending: 0,
  Confirmed: 1,
  Shipped: 2,
  Delivered: 3,
  Cancelled: 4,
} as const
export type OrderStatusValue = (typeof OrderStatus)[keyof typeof OrderStatus]

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export interface ApiErrorResponse {
  status: number
  message: string
  errors: Record<string, string[]>
}

export type UserRole = 'Admin' | 'Staff'

export interface ManagedUser {
  id: string
  fullName: string
  email: string
  roles: UserRole[]
  isActive: boolean
  isLockedOut: boolean
  createdAt: string
}

export interface CreateUserInput {
  fullName: string
  email: string
  password: string
  role: UserRole
}

export interface AuthSession {
  token: string
  expiresAt: string
  email: string
  fullName: string
  roles: string[]
}

export interface RegisterInput {
  fullName: string
  email: string
  password: string
}

export interface RegistrationStatus {
  enabled: boolean
}

export interface Product {
  id: number
  name: string
  description: string | null
  sku: string
  price: number
  stockQuantity: number
  reorderLevel: number
  lowStock: boolean
  categoryId: number
  categoryName: string
  isActive: boolean
  createdAt: string
}

export interface ProductInput {
  name: string
  description: string | null
  sku: string
  price: number
  stockQuantity: number
  reorderLevel: number
  categoryId: number
}

export interface ProductUpdateInput {
  name: string
  description: string | null
  price: number
  reorderLevel: number
  categoryId: number
  isActive: boolean
}

export interface StockAdjustInput {
  quantityChange: number
  reason: string
}

export interface ProductListParams {
  pageNumber?: number
  pageSize?: number
  search?: string
  categoryId?: number
  lowStockOnly?: boolean
  sortBy?: 'name' | 'price' | 'stock' | 'createdAt'
  sortDescending?: boolean
}

export interface Category {
  id: number
  name: string
  description: string | null
  productCount: number
}

export interface Customer {
  id: number
  fullName: string
  email: string
  phone: string | null
  address: string | null
  createdAt: string
}

export interface CustomerInput {
  fullName: string
  email: string
  phone: string | null
  address: string | null
}

export interface OrderLine {
  productId: number
  productName: string
  quantity: number
  unitPrice: number
  lineTotal: number
}

export interface Order {
  id: number
  orderNumber: string
  customerId: number
  customerName: string
  status: OrderStatusValue
  totalAmount: number
  orderDate: string
  items: OrderLine[]
}

export interface OrderInput {
  customerId: number
  items: { productId: number; quantity: number }[]
}

export interface OrderListParams extends PageParams {
  search?: string
  status?: OrderStatusValue
  sortBy?: 'orderDate' | 'orderNumber' | 'customer' | 'total'
  sortDescending?: boolean
}

export interface PageParams {
  pageNumber?: number
  pageSize?: number
}

export interface OrderStatusCount {
  status: OrderStatusValue
  count: number
}

export interface InventorySummary {
  windowDays: number
  activeProducts: number
  totalUnitsOnHand: number
  inventoryValue: number
  outOfStockCount: number
  needsReorderCount: number
  overstockedCount: number
  deadStockCount: number
  slowStockValue: number
  ordersInWindow: number
  revenueInWindow: number
  ordersByStatus: OrderStatusCount[]
}

export type StockHealth = 'OutOfStock' | 'Critical' | 'ReorderSoon' | 'Healthy' | 'Overstocked' | 'Dead'

export interface ReorderSuggestion {
  productId: number
  name: string
  sku: string
  categoryName: string
  price: number
  stockOnHand: number
  reorderLevel: number
  unitsSold: number
  windowDays: number
  dailyDemand: number
  daysOfCover: number | null
  reorderPoint: number
  suggestedOrderQuantity: number
  health: StockHealth
}

export interface ReorderParams {
  lookbackDays?: number
  leadTimeDays?: number
  safetyDays?: number
  limit?: number
  includeHealthy?: boolean
}

export interface SalesTrendPoint {
  date: string
  revenue: number
  orders: number
}
