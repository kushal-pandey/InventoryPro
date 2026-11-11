import { api } from './client'
import type {
  AuthSession,
  Category,
  CreateUserInput,
  Customer,
  CustomerInput,
  InventorySummary,
  ManagedUser,
  Order,
  OrderInput,
  OrderListParams,
  OrderStatusValue,
  PageParams,
  PagedResult,
  Product,
  ProductInput,
  ProductListParams,
  ProductUpdateInput,
  RegisterInput,
  RegistrationStatus,
  ReorderParams,
  ReorderSuggestion,
  SalesTrendPoint,
  StockAdjustInput,
  UserRole,
} from './types'

export const authApi = {
  registrationStatus: () =>
    api.get<RegistrationStatus>('/auth/registration-status').then((r) => r.data),
  login: (email: string, password: string) =>
    api.post<AuthSession>('/auth/login', { email, password }).then((r) => r.data),
  register: (body: RegisterInput) =>
    api.post<AuthSession>('/auth/register', body).then((r) => r.data),
}

export const usersApi = {
  list: (params: PageParams = {}) =>
    api.get<PagedResult<ManagedUser>>('/users', { params }).then((r) => r.data),
  create: (body: CreateUserInput) => api.post<ManagedUser>('/users', body).then((r) => r.data),
  updateRole: (id: string, role: UserRole) =>
    api.patch<ManagedUser>(`/users/${encodeURIComponent(id)}/role`, { role }).then((r) => r.data),
  updateActive: (id: string, isActive: boolean) =>
    api.patch<ManagedUser>(`/users/${encodeURIComponent(id)}/active`, { isActive }).then((r) => r.data),
}

export const productsApi = {
  list: (params: ProductListParams) =>
    api.get<PagedResult<Product>>('/products', { params }).then((r) => r.data),
  lowStock: (params: PageParams = {}) =>
    api.get<PagedResult<Product>>('/products/low-stock', { params }).then((r) => r.data),
  create: (body: ProductInput) => api.post<Product>('/products', body).then((r) => r.data),
  update: (id: number, body: ProductUpdateInput) => api.put(`/products/${id}`, body).then(() => undefined),
  adjustStock: (id: number, body: StockAdjustInput) =>
    api.patch<Product>(`/products/${id}/stock`, body).then((r) => r.data),
  remove: (id: number) => api.delete(`/products/${id}`).then(() => undefined),
}

export const categoriesApi = {
  list: (params: PageParams = {}) =>
    api.get<PagedResult<Category>>('/categories', { params }).then((r) => r.data),
  create: (body: { name: string; description: string | null }) =>
    api.post<Category>('/categories', body).then((r) => r.data),
  remove: (id: number) => api.delete(`/categories/${id}`).then(() => undefined),
}

export const customersApi = {
  list: (params: PageParams = {}) =>
    api.get<PagedResult<Customer>>('/customers', { params }).then((r) => r.data),
  create: (body: CustomerInput) => api.post<Customer>('/customers', body).then((r) => r.data),
}

export const ordersApi = {
  list: (params: OrderListParams = {}) =>
    api.get<PagedResult<Order>>('/orders', { params }).then((r) => r.data),
  create: (body: OrderInput) => api.post<Order>('/orders', body).then((r) => r.data),
  updateStatus: (id: number, status: OrderStatusValue) =>
    api.patch<Order>(`/orders/${id}/status`, { status }).then((r) => r.data),
}

export const insightsApi = {
  summary: () => api.get<InventorySummary>('/insights/summary').then((r) => r.data),
  reorder: (params: ReorderParams = {}) =>
    api.get<ReorderSuggestion[]>('/insights/reorder-suggestions', { params }).then((r) => r.data),
  salesTrend: (days = 30) =>
    api.get<SalesTrendPoint[]>('/insights/sales-trend', { params: { days } }).then((r) => r.data),
}
