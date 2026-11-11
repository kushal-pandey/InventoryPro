import { useCallback, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CheckSquare, ClipboardCopy, Download, Printer } from 'lucide-react'
import { insightsApi } from '../api/endpoints'
import type { ReorderSuggestion, StockHealth } from '../api/types'
import { Field } from '../components/Field'
import { HealthBadge } from '../components/Badges'
import { PageHeader } from '../components/PageHeader'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { useToast } from '../components/useToast'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useQuery } from '../hooks/useQuery'
import { downloadCsv } from '../lib/csv'
import { formatMoney, formatNumber } from '../lib/format'
import { buildSelectedPurchaseList, selectAllNeedingReorder, toggleReorderSelection } from '../lib/reorderSelection'
import { updateListUrl } from '../lib/listUrlState'

function bounded(input: string, min: number, max: number, fallback: number): number {
  const parsed = Number.parseInt(input, 10)
  if (Number.isNaN(parsed)) return fallback
  return Math.min(max, Math.max(min, parsed))
}

const today = () => new Date().toISOString().slice(0, 10)

function buildMessage(items: ReorderSuggestion[]): string {
  const lines = items.map((item) => `• ${item.name} (${item.sku}) – ${item.suggestedOrderQuantity}`)
  return [`Reorder list – ${today()}`, ...lines].join('\n')
}

const HEALTH_FILTERS: { value: string; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'needs-reorder', label: 'Needs reorder' },
  { value: 'OutOfStock', label: 'Out of stock' },
  { value: 'Critical', label: 'Critical' },
  { value: 'ReorderSoon', label: 'Reorder soon' },
  { value: 'Healthy', label: 'Healthy' },
  { value: 'Overstocked', label: 'Overstocked' },
  { value: 'Dead', label: 'Not selling' },
]

export default function ReorderPlannerPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const { notify } = useToast()
  const requestedStatus = searchParams.get('status') ?? 'all'
  const statusFilter = HEALTH_FILTERS.some((filter) => filter.value === requestedStatus) ? requestedStatus : 'all'
  const [lookback, setLookback] = useState('30')
  const [leadTime, setLeadTime] = useState('7')
  const [safety, setSafety] = useState('3')
  const [selectedIds, setSelectedIds] = useState<number[]>([])
  const [quantities, setQuantities] = useState<Record<number, string>>({})

  // Debounce a primitive key (not an object literal, whose identity changes on every render).
  const debouncedKey = useDebouncedValue(`${lookback}|${leadTime}|${safety}`, 450)
  const params = useMemo(() => {
    const [lookbackInput, leadInput, safetyInput] = debouncedKey.split('|')
    return {
      lookbackDays: bounded(lookbackInput, 7, 180, 30),
      leadTimeDays: bounded(leadInput, 0, 60, 7),
      safetyDays: bounded(safetyInput, 0, 60, 3),
      limit: 200,
      includeHealthy: true,
    }
  }, [debouncedKey])

  const query = useQuery(useCallback(() => insightsApi.reorder(params), [params]))
  const items = useMemo(() => query.data ?? [], [query.data])
  const toOrder = useMemo(() => items.filter((item) => item.suggestedOrderQuantity > 0), [items])
  const visibleItems = useMemo(() => items.filter((item) => {
    if (statusFilter === 'all') return true
    if (statusFilter === 'needs-reorder') return item.suggestedOrderQuantity > 0
    return item.health === statusFilter
  }), [items, statusFilter])
  const selectedPurchaseList = useMemo(
    () => buildSelectedPurchaseList(items, selectedIds, quantities),
    [items, selectedIds, quantities],
  )
  const counts = useMemo(() => {
    const result: Partial<Record<StockHealth, number>> = {}
    for (const item of items) result[item.health] = (result[item.health] ?? 0) + 1
    return result
  }, [items])
  const unitsToBuy = selectedPurchaseList.reduce((sum, item) => sum + item.suggestedOrderQuantity, 0)
  const allNeedSelected = toOrder.length > 0 && toOrder.every((item) => selectedIds.includes(item.productId))

  function setStatusFilter(value: string) {
    setSearchParams(updateListUrl(searchParams, { status: value === 'all' ? null : value }))
  }

  function exportCsv() {
    downloadCsv(`reorder-list-${today()}.csv`, [
      ['SKU', 'Product', 'Category', 'On hand', 'Sold (window)', 'Daily demand', 'Days of cover', 'Order quantity'],
      ...selectedPurchaseList.map((item) => [
        item.sku,
        item.name,
        item.categoryName,
        item.stockOnHand,
        item.unitsSold,
        item.dailyDemand,
        item.daysOfCover ?? '',
        item.suggestedOrderQuantity,
      ]),
    ])
  }

  async function copyMessage() {
    try {
      await navigator.clipboard.writeText(buildMessage(selectedPurchaseList))
      notify('Supplier message copied. Paste it into WhatsApp, SMS or email.')
    } catch (error) {
      notify('Could not access the clipboard. Use the CSV export instead.', 'error')
      console.warn('Could not copy the supplier message to the clipboard.', error)
    }
  }

  return (
    <>
      <PageHeader
        title="Reorder planner"
        subtitle="What to buy, how much, and what is sitting unsold, based on how fast each product actually sells."
        actions={
          <>
            <button type="button" className="btn btn-ghost" onClick={copyMessage} disabled={selectedPurchaseList.length === 0}>
              <ClipboardCopy size={16} /> Copy supplier message
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => window.print()} disabled={selectedPurchaseList.length === 0}>
              <Printer size={16} /> Print list
            </button>
            <button type="button" className="btn btn-primary" onClick={exportCsv} disabled={selectedPurchaseList.length === 0}>
              <Download size={16} /> Export CSV
            </button>
          </>
        }
      />

      <section className="panel panel-pad">
        <div className="form-row">
          <Field label="Sales history (days)" htmlFor="lookback" hint="7–180. Longer smooths out spikes.">
            <input id="lookback" type="number" min={7} max={180} value={lookback} onChange={(e) => setLookback(e.target.value)} />
          </Field>
          <Field label="Supplier lead time (days)" htmlFor="lead" hint="Order to delivery.">
            <input id="lead" type="number" min={0} max={60} value={leadTime} onChange={(e) => setLeadTime(e.target.value)} />
          </Field>
          <Field label="Safety buffer (days)" htmlFor="safety" hint="Extra cover for delays.">
            <input id="safety" type="number" min={0} max={60} value={safety} onChange={(e) => setSafety(e.target.value)} />
          </Field>
        </div>

        <details className="explainer">
          <summary>How are these numbers calculated?</summary>
          <ul>
            <li>
              <strong>Daily demand</strong> = units sold in the sales history ÷ days (cancelled orders are ignored).
            </li>
            <li>
              <strong>Days of cover</strong> = stock on hand ÷ daily demand.
            </li>
            <li>
              <strong>Reorder point</strong> = daily demand × (lead time + safety buffer), never below the product&apos;s own
              reorder level.
            </li>
            <li>
              <strong>Order quantity</strong> tops stock up to cover lead time, safety and another 14 days of sales.
            </li>
            <li>
              <strong>Critical</strong> means the stock will run out before a new order could arrive.{' '}
              <strong>Not selling</strong> means no sales for 60+ days; it is never suggested for reorder.
            </li>
          </ul>
        </details>
      </section>

      {query.error && <ErrorBanner message={query.error} onRetry={query.reload} />}

      {query.data && (
        <div className="chips" role="group" aria-label="Summary">
          <span className="chip">{toOrder.length} to order</span>
          <span className="chip">{selectedPurchaseList.length} selected · {formatNumber(unitsToBuy)} units</span>
          {(counts.OutOfStock ?? 0) > 0 && <span className="chip chip-red">{counts.OutOfStock} out of stock</span>}
          {(counts.Critical ?? 0) > 0 && <span className="chip chip-red">{counts.Critical} critical</span>}
          {(counts.Overstocked ?? 0) + (counts.Dead ?? 0) > 0 && (
            <span className="chip chip-grey">{(counts.Overstocked ?? 0) + (counts.Dead ?? 0)} slow-moving</span>
          )}
        </div>
      )}

      <section className="panel">
        {!query.data && !query.error && <LoadingBlock />}
        {query.data && items.length === 0 && (
          <EmptyState title="Nothing to reorder">
            Every product has enough stock for its recent sales. <Link to="/products" className="link">Review inventory</Link>
          </EmptyState>
        )}
          {query.data && visibleItems.length === 0 && items.length > 0 && (
            <EmptyState title="No products in this status">
              <button type="button" className="btn btn-ghost" onClick={() => setStatusFilter('all')}>Show all statuses</button>
            </EmptyState>
          )}
          {query.data && items.length > 0 && (
            <>
              <div className="panel-pad status-filters" role="group" aria-label="Filter products by stock status">
                {HEALTH_FILTERS.map((filter) => {
                  const count = filter.value === 'all'
                    ? items.length
                    : filter.value === 'needs-reorder'
                      ? toOrder.length
                      : counts[filter.value as StockHealth] ?? 0
                  return (
                    <button
                      key={filter.value}
                      type="button"
                      className="status-filter"
                      aria-pressed={statusFilter === filter.value}
                      onClick={() => setStatusFilter(filter.value)}
                    >
                      {filter.label} <span>{count}</span>
                    </button>
                  )
                })}
              </div>
              <div className="selection-toolbar">
                <button
                  type="button"
                  className="btn btn-ghost"
                  onClick={() => setSelectedIds((current) => selectAllNeedingReorder(items, current))}
                  disabled={toOrder.length === 0}
                >
                  <CheckSquare size={16} /> {allNeedSelected ? 'Clear reorder selection' : 'Select all needing reorder'}
                </button>
                <span className="muted">{selectedPurchaseList.length} selected · {formatNumber(unitsToBuy)} units to order</span>
              </div>
            </>
          )}
          {query.data && visibleItems.length > 0 && (
            <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th><span className="visually-hidden">Select for purchase list</span></th>
                  <th>Product</th>
                  <th className="num">On hand</th>
                  <th className="num">Sold</th>
                  <th className="num">Per day</th>
                  <th className="num">Days left</th>
                  <th>Status</th>
                  <th className="num">Order</th>
                  <th className="num">Stock value</th>
                </tr>
              </thead>
              <tbody>
                {visibleItems.map((item) => (
                  <tr key={item.productId}>
                    <td data-label="Select">
                      <input
                        type="checkbox"
                        className="selection-checkbox"
                        aria-label={`Select ${item.name} for purchase list`}
                        checked={selectedIds.includes(item.productId)}
                        disabled={item.suggestedOrderQuantity <= 0}
                        onChange={() => setSelectedIds((current) => toggleReorderSelection(current, item.productId))}
                      />
                    </td>
                    <td data-label="Product">
                      <strong>{item.name}</strong>
                      <span className="sub">
                        {item.sku} · {item.categoryName}
                      </span>
                    </td>
                    <td data-label="On hand" className="num">{formatNumber(item.stockOnHand)}</td>
                    <td data-label="Sold" className="num">{formatNumber(item.unitsSold)}</td>
                    <td data-label="Per day" className="num">{item.dailyDemand}</td>
                    <td data-label="Days left" className="num">{item.daysOfCover === null ? '—' : item.daysOfCover}</td>
                    <td data-label="Status">
                      <HealthBadge health={item.health} />
                    </td>
                    <td data-label="Order" className="num">
                      {item.suggestedOrderQuantity > 0 ? (
                        <input
                          className="qty"
                          type="number"
                          min={1}
                          step={1}
                          aria-label={`Order quantity for ${item.name}`}
                          value={quantities[item.productId] ?? String(item.suggestedOrderQuantity)}
                          onChange={(event) => setQuantities((current) => ({ ...current, [item.productId]: event.target.value }))}
                        />
                      ) : '—'}
                    </td>
                    <td data-label="Stock value" className="num">{formatMoney(item.stockOnHand * item.price)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      {selectedPurchaseList.length > 0 && (
        <section id="printable-purchase-list" className="purchase-list-print" aria-label="Selected purchase list">
          <h1>Purchase list · {today()}</h1>
          <p>Selected products and requested order quantities.</p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>SKU</th><th>Product</th><th>Category</th><th className="num">On hand</th><th className="num">Order quantity</th></tr>
              </thead>
              <tbody>
                {selectedPurchaseList.map((item) => (
                  <tr key={item.productId}>
                    <td>{item.sku}</td><td>{item.name}</td><td>{item.categoryName}</td>
                    <td className="num">{item.stockOnHand}</td><td className="num">{item.suggestedOrderQuantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      </section>
    </>
  )
}
