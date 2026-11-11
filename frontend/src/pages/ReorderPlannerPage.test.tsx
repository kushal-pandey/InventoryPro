import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import type { ReorderSuggestion } from '../api/types'
import { insightsApi } from '../api/endpoints'
import ReorderPlannerPage from './ReorderPlannerPage'
import { ToastProvider } from '../components/ToastProvider'

vi.mock('../api/endpoints', () => ({ insightsApi: { reorder: vi.fn() } }))

const suggestion = (overrides: Partial<ReorderSuggestion> = {}): ReorderSuggestion => ({
  productId: 1,
  name: 'Wireless Mouse',
  sku: 'ELEC-001',
  categoryName: 'Electronics',
  price: 799,
  stockOnHand: 5,
  reorderLevel: 5,
  unitsSold: 60,
  windowDays: 30,
  dailyDemand: 2,
  daysOfCover: 2.5,
  reorderPoint: 20,
  suggestedOrderQuantity: 43,
  health: 'Critical',
  ...overrides,
})

describe('ReorderPlannerPage', () => {
  function renderPlanner(initialEntry = '/reorder') {
    return render(
      <MemoryRouter initialEntries={[initialEntry]}>
        <ToastProvider><ReorderPlannerPage /></ToastProvider>
      </MemoryRouter>,
    )
  }

  it('lists suggestions with the quantity to order and enables the exports', async () => {
    vi.mocked(insightsApi.reorder).mockResolvedValue([suggestion()])
    renderPlanner()

    expect(await screen.findByText('Wireless Mouse')).toBeTruthy()
    const table = within(screen.getByRole('table'))
    expect((screen.getByRole('spinbutton', { name: 'Order quantity for Wireless Mouse' }) as HTMLInputElement).value).toBe('43')
    expect(table.getByText('Critical')).toBeTruthy()
    expect((screen.getByRole('button', { name: /export csv/i }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Order quantity for Wireless Mouse' }), { target: { value: '12' } })
    fireEvent.click(screen.getByRole('button', { name: /select all needing reorder/i }))
    expect((screen.getByRole('button', { name: /export csv/i }) as HTMLButtonElement).disabled).toBe(false)
    expect(screen.getByText('1 selected · 12 units to order')).toBeTruthy()
  })

  it('applies a health filter linked from a dashboard query string', async () => {
    vi.mocked(insightsApi.reorder).mockResolvedValue([suggestion(), suggestion({
      productId: 2,
      name: 'Rice',
      sku: 'FOOD-2',
      health: 'Healthy',
      suggestedOrderQuantity: 0,
    })])
    renderPlanner('/reorder?status=Critical')

    expect(await screen.findByText('Wireless Mouse')).toBeTruthy()
    expect(screen.queryByText('Rice')).toBeNull()
    expect((screen.getByRole('button', { name: /critical/i }) as HTMLButtonElement).getAttribute('aria-pressed')).toBe('true')
  })

  it('shows an empty state and disables exports when nothing needs ordering', async () => {
    vi.mocked(insightsApi.reorder).mockResolvedValue([])
    renderPlanner()

    expect(await screen.findByText('Nothing to reorder')).toBeTruthy()
    expect((screen.getByRole('button', { name: /export csv/i }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('requests data once and does not refetch in a loop', async () => {
    vi.mocked(insightsApi.reorder).mockResolvedValue([suggestion()])
    renderPlanner()
    await screen.findByText('Wireless Mouse')

    // Wait longer than the 450ms debounce window; a re-render loop would trigger more requests.
    await new Promise((resolve) => setTimeout(resolve, 1200))
    await waitFor(() => expect(insightsApi.reorder).toHaveBeenCalledTimes(1))
    expect(insightsApi.reorder).toHaveBeenCalledWith(
      expect.objectContaining({ lookbackDays: 30, leadTimeDays: 7, safetyDays: 3, includeHealthy: true }),
    )
  })

  it('shows the error with a retry action when the API fails', async () => {
    vi.mocked(insightsApi.reorder).mockRejectedValue(new Error('server down'))
    renderPlanner()

    expect(await screen.findByText('server down')).toBeTruthy()
    expect(screen.getByRole('button', { name: /retry/i })).toBeTruthy()
  })
})
