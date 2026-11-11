import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { axe } from 'vitest-axe'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextValue } from './auth/AuthContext'
import { categoriesApi, customersApi, insightsApi, ordersApi, productsApi } from './api/endpoints'
import type { InventorySummary, Order, Product, ReorderSuggestion } from './api/types'
import { ToastProvider } from './components/ToastProvider'
import DashboardPage from './pages/DashboardPage'
import OrdersPage from './pages/OrdersPage'
import ProductsPage from './pages/ProductsPage'
import ReorderPlannerPage from './pages/ReorderPlannerPage'

vi.mock('./api/endpoints', () => ({
  categoriesApi: { list: vi.fn() },
  customersApi: { list: vi.fn() },
  insightsApi: { summary: vi.fn(), reorder: vi.fn(), salesTrend: vi.fn() },
  ordersApi: { list: vi.fn(), create: vi.fn(), updateStatus: vi.fn() },
  productsApi: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), adjustStock: vi.fn() },
  usersApi: { list: vi.fn(), create: vi.fn(), updateRole: vi.fn(), updateActive: vi.fn() },
}))

const adminContext: AuthContextValue = {
  session: {
    token: 'test-token',
    expiresAt: '2099-01-01T00:00:00Z',
    email: 'admin@example.test',
    fullName: 'Test Admin',
    roles: ['Admin'],
  },
  isAdmin: true,
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

const product: Product = {
  id: 1,
  name: 'Tea',
  description: null,
  sku: 'TEA-1',
  price: 4,
  stockQuantity: 2,
  reorderLevel: 5,
  lowStock: true,
  categoryId: 1,
  categoryName: 'Drinks',
  isActive: true,
  createdAt: '2026-11-09T10:00:00Z',
}

const order: Order = {
  id: 1,
  orderNumber: 'ORD-1',
  customerId: 1,
  customerName: 'Test Customer',
  status: 0,
  totalAmount: 8,
  orderDate: '2026-11-09T10:00:00Z',
  items: [{ productId: 1, productName: 'Tea', quantity: 2, unitPrice: 4, lineTotal: 8 }],
}

const suggestion: ReorderSuggestion = {
  productId: 1,
  name: 'Tea',
  sku: 'TEA-1',
  categoryName: 'Drinks',
  price: 4,
  stockOnHand: 2,
  reorderLevel: 5,
  unitsSold: 20,
  windowDays: 30,
  dailyDemand: 0.67,
  daysOfCover: 3,
  reorderPoint: 7,
  suggestedOrderQuantity: 8,
  health: 'Critical',
}

const summary: InventorySummary = {
  windowDays: 30,
  activeProducts: 1,
  totalUnitsOnHand: 2,
  inventoryValue: 8,
  outOfStockCount: 0,
  needsReorderCount: 1,
  overstockedCount: 0,
  deadStockCount: 0,
  slowStockValue: 0,
  ordersInWindow: 1,
  revenueInWindow: 8,
  ordersByStatus: [],
}

function paged<T>(items: T[]) {
  return { items, pageNumber: 1, pageSize: 10, totalCount: items.length, totalPages: 1, hasPrevious: false, hasNext: false }
}

function renderPage(page: ReactNode, path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={adminContext}>
        <ToastProvider>{page}</ToastProvider>
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

async function expectAccessible(container: HTMLElement) {
  const previousTheme = document.documentElement.dataset.theme
  try {
    for (const theme of ['light', 'dark']) {
      document.documentElement.dataset.theme = theme
      const results = await axe(container, {
        runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
      })
      expect(results.violations, `${theme} theme: ${JSON.stringify(results.violations, null, 2)}`).toHaveLength(0)
    }
  } finally {
    if (previousTheme) document.documentElement.dataset.theme = previousTheme
    else delete document.documentElement.dataset.theme
  }
}

describe('key-page accessibility', () => {
  beforeEach(() => {
    vi.mocked(categoriesApi.list).mockResolvedValue(paged([]))
    vi.mocked(productsApi.list).mockResolvedValue(paged([product]))
    vi.mocked(ordersApi.list).mockResolvedValue(paged([order]))
    vi.mocked(insightsApi.summary).mockResolvedValue(summary)
    vi.mocked(insightsApi.salesTrend).mockResolvedValue([{ date: '2026-11-09', revenue: 8, orders: 1 }])
    vi.mocked(insightsApi.reorder).mockResolvedValue([suggestion])
    vi.mocked(customersApi.list).mockResolvedValue(paged([]))
  })

  it('keeps the dashboard accessible', async () => {
    const { container } = renderPage(<DashboardPage />, '/')
    expect(await screen.findByText('Dashboard')).toBeTruthy()
    await screen.findByText('Tea')
    await expectAccessible(container)
  })

  it('updates the dashboard sales range and refreshes on focus', async () => {
    renderPage(<DashboardPage />, '/')
    await screen.findByText(/Last updated/)

    fireEvent.change(screen.getByRole('combobox', { name: 'Sales date range' }), { target: { value: '7' } })
    expect(await screen.findByRole('heading', { name: 'Sales, last 7 days' })).toBeTruthy()
    await waitFor(() => expect(insightsApi.salesTrend).toHaveBeenCalledWith(7))

    const callsBeforeFocus = vi.mocked(insightsApi.salesTrend).mock.calls.length
    window.dispatchEvent(new Event('focus'))
    await waitFor(() => expect(vi.mocked(insightsApi.salesTrend).mock.calls.length).toBeGreaterThan(callsBeforeFocus))
  })

  it('keeps Products accessible', async () => {
    const { container } = renderPage(<ProductsPage />, '/products')
    expect(await screen.findByText('TEA-1')).toBeTruthy()
    await expectAccessible(container)
  })

  it('keeps Orders accessible', async () => {
    const { container } = renderPage(<OrdersPage />, '/orders')
    expect(await screen.findByText('ORD-1')).toBeTruthy()
    await expectAccessible(container)
  })

  it('keeps the reorder planner accessible', async () => {
    const { container } = renderPage(<ReorderPlannerPage />, '/reorder')
    expect(await screen.findByText('Tea')).toBeTruthy()
    await expectAccessible(container)
  })
})
