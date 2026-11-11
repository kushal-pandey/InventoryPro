import { useCallback, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowDown, ArrowUp, ArrowUpDown, Eye, Plus, Trash2 } from 'lucide-react'
import { errorMessage } from '../api/errors'
import { customersApi, ordersApi, productsApi } from '../api/endpoints'
import type { Order, OrderListParams, OrderStatusValue, Product } from '../api/types'
import { OrderStatus } from '../api/types'
import { OrderStatusBadge } from '../components/Badges'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Drawer } from '../components/Drawer'
import { Field } from '../components/Field'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'
import { Pagination } from '../components/Pagination'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useQuery } from '../hooks/useQuery'
import { useSearchShortcut } from '../hooks/useSearchShortcut'
import { formatDateTime, formatMoney } from '../lib/format'
import { readOrderListUrl, updateListUrl } from '../lib/listUrlState'
import { NEXT_STATUSES, ORDER_STATUS_LABEL } from '../lib/orderStatus'
import { useToast } from '../components/useToast'
import { beginOrderStatusChange, resolveOrderStatusChange } from '../lib/optimisticOrderStatus'

const PAGE_SIZE = 10

type Dialog = { kind: 'new' } | { kind: 'detail'; order: Order } | { kind: 'cancel'; order: Order } | null

function SortableHeader({
  label,
  field,
  sort,
  onSort,
  numeric = false,
}: {
  label: string
  field: NonNullable<OrderListParams['sortBy']>
  sort: string
  onSort: (sort: string) => void
  numeric?: boolean
}) {
  const [currentField, direction] = sort.split(':')
  const active = currentField === field
  const nextDirection = active && direction === 'asc' ? 'desc' : 'asc'
  return (
    <th className={numeric ? 'num' : undefined} aria-sort={active ? (direction === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button type="button" className="sort-header" onClick={() => onSort(`${field}:${nextDirection}`)}>
        {label}
        {active ? direction === 'asc' ? <ArrowUp size={14} aria-hidden="true" /> : <ArrowDown size={14} aria-hidden="true" /> : <ArrowUpDown size={14} aria-hidden="true" />}
      </button>
    </th>
  )
}

export default function OrdersPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const { page, search, status, sort } = readOrderListUrl(searchParams)
  const { notify } = useToast()
  const searchRef = useRef<HTMLInputElement>(null)
  useSearchShortcut(searchRef)
  const [dialog, setDialog] = useState<Dialog>(null)
  const [busyId, setBusyId] = useState<number | null>(null)
  const [optimisticStatuses, setOptimisticStatuses] = useState<Record<number, OrderStatusValue>>({})
  const debouncedSearch = useDebouncedValue(search.trim())
  const [sortBy, direction] = sort.split(':') as [NonNullable<OrderListParams['sortBy']>, 'asc' | 'desc']

  function updateUrl(updates: Record<string, string | number | boolean | null>, resetPage = false, replace = false) {
    setSearchParams(updateListUrl(searchParams, updates, resetPage), { replace })
  }

  const orders = useQuery(
    useCallback(
      () => ordersApi.list({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        search: debouncedSearch || undefined,
        status: status === '' ? undefined : Number(status) as OrderStatusValue,
        sortBy,
        sortDescending: direction === 'desc',
      }),
      [page, debouncedSearch, status, sortBy, direction],
    ),
  )
  const data = orders.data

  async function applyStatus(order: Order, nextStatus: OrderStatusValue, toastError = true) {
    const change = beginOrderStatusChange(order, nextStatus)
    setBusyId(order.id)
    setOptimisticStatuses((current) => ({ ...current, [order.id]: change.nextStatus }))
    try {
      const saved = await ordersApi.updateStatus(order.id, nextStatus)
      setOptimisticStatuses((current) => ({ ...current, [order.id]: resolveOrderStatusChange(change, saved.status) }))
      notify(
        nextStatus === OrderStatus.Cancelled
          ? `Order ${order.orderNumber} cancelled and stock restored.`
          : `Order ${order.orderNumber} is now ${ORDER_STATUS_LABEL[nextStatus].toLowerCase()}.`,
      )
      orders.reload()
    } catch (err) {
      setOptimisticStatuses((current) => ({ ...current, [order.id]: resolveOrderStatusChange(change) }))
      if (toastError) notify(errorMessage(err), 'error')
      else throw err
    } finally {
      setBusyId(null)
    }
  }

  function requestStatus(order: Order, status: OrderStatusValue) {
    if (status === OrderStatus.Cancelled) setDialog({ kind: 'cancel', order })
    else void applyStatus(order, status)
  }

  return (
    <>
      <PageHeader
        title="Orders"
        subtitle="Stock is reserved when an order is placed and returned if it is cancelled."
        actions={
          <button type="button" className="btn btn-primary" onClick={() => setDialog({ kind: 'new' })}>
            <Plus size={16} /> New order
          </button>
        }
      />

      {orders.error && <ErrorBanner message={orders.error} onRetry={orders.reload} />}

      <section className="panel panel-pad">
        <div className="toolbar">
          <div className="search">
            <input
              ref={searchRef}
              type="search"
              placeholder="Search order, customer, or product"
              aria-label="Search orders"
              value={search}
              onChange={(event) => updateUrl({ q: event.target.value }, true, true)}
            />
          </div>
          <select
            aria-label="Filter by order status"
            value={status}
            onChange={(event) => updateUrl({ status: event.target.value || null }, true)}
          >
            <option value="">All statuses</option>
            {Object.entries(ORDER_STATUS_LABEL).map(([value, label]) => (
              <option key={value} value={value}>{label}</option>
            ))}
          </select>
        </div>
      </section>

      <section className="panel">
        {!data && !orders.error && <LoadingBlock />}
        {data && data.items.length === 0 && (
          <EmptyState title={search || status ? 'No matching orders' : 'No orders yet'}>
            {search || status
              ? <button type="button" className="btn btn-ghost" onClick={() => setSearchParams(new URLSearchParams())}>Clear search and filters</button>
              : <button type="button" className="btn btn-primary" onClick={() => setDialog({ kind: 'new' })}><Plus size={16} /> Create first order</button>}
          </EmptyState>
        )}
        {data && data.items.length > 0 && (
          <div className={orders.loading ? 'table-wrap is-loading' : 'table-wrap'}>
            <table>
              <thead>
                <tr>
                  <SortableHeader label="Order" field="orderNumber" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} />
                  <SortableHeader label="Customer" field="customer" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} />
                  <SortableHeader label="Placed" field="orderDate" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} />
                  <th className="num">Items</th>
                  <SortableHeader label="Total" field="total" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} numeric />
                  <th>Status</th>
                  <th className="actions-col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((originalOrder) => {
                  const order = orders.loading && optimisticStatuses[originalOrder.id] !== undefined
                    ? { ...originalOrder, status: optimisticStatuses[originalOrder.id] }
                    : originalOrder
                  const next = NEXT_STATUSES[order.status]
                  return (
                    <tr key={order.id}>
                      <td data-label="Order">
                        <strong>{order.orderNumber}</strong>
                      </td>
                      <td data-label="Customer">{order.customerName}</td>
                      <td data-label="Placed">{formatDateTime(order.orderDate)}</td>
                      <td data-label="Items" className="num">{order.items.reduce((sum, line) => sum + line.quantity, 0)}</td>
                      <td data-label="Total" className="num">{formatMoney(order.totalAmount)}</td>
                      <td data-label="Status">
                        <OrderStatusBadge status={order.status} />
                      </td>
                      <td data-label="Actions" className="actions-col">
                        <button type="button" className="icon-button" aria-label={`View ${order.orderNumber}`} title="View" onClick={() => setDialog({ kind: 'detail', order })}>
                          <Eye size={16} />
                        </button>
                        {next.length > 0 && (
                          <select
                            className="select-sm"
                            aria-label={`Change status of ${order.orderNumber}`}
                            value=""
                            disabled={busyId === order.id}
                            onChange={(event) => requestStatus(order, Number(event.target.value) as OrderStatusValue)}
                          >
                            <option value="" disabled>
                              Move to…
                            </option>
                            {next.map((status) => (
                              <option key={status} value={status}>
                                {ORDER_STATUS_LABEL[status]}
                              </option>
                            ))}
                          </select>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
        {data && <Pagination page={data.pageNumber} totalPages={data.totalPages} totalCount={data.totalCount} onChange={(next) => updateUrl({ page: next })} />}
      </section>

      {dialog?.kind === 'new' && (
        <NewOrderModal
          onClose={() => setDialog(null)}
          onCreated={(order) => {
            setDialog(null)
            notify(`Order ${order.orderNumber} placed.`)
            updateUrl({ page: null })
            orders.reload()
          }}
        />
      )}

      {dialog?.kind === 'detail' && (
        <Drawer title={dialog.order.orderNumber} onClose={() => setDialog(null)}>
          <p className="modal-text">
            {dialog.order.customerName} · {formatDateTime(dialog.order.orderDate)} · <OrderStatusBadge status={dialog.order.status} />
          </p>
          {dialog.order.status === OrderStatus.Cancelled && (
            <p className="stock-restored" role="status">Stock from this order has been restored to inventory.</p>
          )}
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Product</th>
                  <th className="num">Qty</th>
                  <th className="num">Unit price</th>
                  <th className="num">Line total</th>
                </tr>
              </thead>
              <tbody>
                {dialog.order.items.map((line) => (
                  <tr key={line.productId}>
                    <td data-label="Product">{line.productName}</td>
                    <td data-label="Qty" className="num">{line.quantity}</td>
                    <td data-label="Unit price" className="num">{formatMoney(line.unitPrice)}</td>
                    <td data-label="Line total" className="num">{formatMoney(line.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3} className="num">
                    <strong>Total</strong>
                  </td>
                  <td className="num">
                    <strong>{formatMoney(dialog.order.totalAmount)}</strong>
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </Drawer>
      )}

      {dialog?.kind === 'cancel' && (
        <ConfirmDialog
          title="Cancel order"
          message={`Cancel ${dialog.order.orderNumber}? The reserved stock goes back into inventory. This cannot be undone.`}
          confirmLabel="Cancel order"
          danger
          onClose={() => setDialog(null)}
          onConfirm={async () => {
            await applyStatus(dialog.order, OrderStatus.Cancelled, false)
            setDialog(null)
          }}
        />
      )}
    </>
  )
}

interface Line {
  product: Product
  quantity: number
}

function NewOrderModal({ onClose, onCreated }: { onClose: () => void; onCreated: (order: Order) => void }) {
  const customers = useQuery(useCallback(() => customersApi.list({ pageSize: 100 }), []))
  const [customerId, setCustomerId] = useState(0)
  const [term, setTerm] = useState('')
  const debounced = useDebouncedValue(term.trim(), 300)
  const results = useQuery(
    useCallback(() => productsApi.list({ search: debounced || undefined, pageSize: 8, sortBy: 'name' }), [debounced]),
  )
  const [lines, setLines] = useState<Line[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const total = lines.reduce((sum, line) => sum + line.product.price * line.quantity, 0)

  function addProduct(product: Product) {
    setLines((current) => {
      const existing = current.find((line) => line.product.id === product.id)
      if (!existing) return [...current, { product, quantity: 1 }]
      return current.map((line) =>
        line.product.id === product.id
          ? { ...line, quantity: Math.min(line.quantity + 1, product.stockQuantity) }
          : line,
      )
    })
  }

  function setQuantity(productId: number, raw: string) {
    setLines((current) =>
      current.map((line) => {
        if (line.product.id !== productId) return line
        const parsed = Number.parseInt(raw, 10)
        const quantity = Number.isNaN(parsed) ? 1 : Math.min(Math.max(parsed, 1), line.product.stockQuantity)
        return { ...line, quantity }
      }),
    )
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (!customerId) return setError('Choose a customer.')
    if (lines.length === 0) return setError('Add at least one product.')

    setBusy(true)
    try {
      const order = await ordersApi.create({
        customerId,
        items: lines.map((line) => ({ productId: line.product.id, quantity: line.quantity })),
      })
      onCreated(order)
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal title="New order" onClose={onClose} wide>
      <form onSubmit={submit} className="form">
        <Field label="Customer" htmlFor="o-customer">
          <select id="o-customer" value={customerId} onChange={(e) => setCustomerId(Number(e.target.value))}>
            <option value={0}>Select a customer…</option>
            {customers.data?.items.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.fullName} · {customer.email}
              </option>
            ))}
          </select>
          {customers.data && customers.data.items.length === 0 && (
            <span className="field-hint">
              No customers yet. <Link to="/customers" className="link">Add one first</Link>.
            </span>
          )}
        </Field>

        <Field label="Add products" htmlFor="o-search" hint="Search by name or SKU, then click a result.">
          <input id="o-search" type="search" placeholder="Search products…" value={term} onChange={(e) => setTerm(e.target.value)} />
        </Field>

        {results.error && <ErrorBanner message={results.error} onRetry={results.reload} />}
        <ul className="picker" aria-label="Product search results">
          {results.data?.items.map((product) => {
            const disabled = !product.isActive || product.stockQuantity <= 0
            return (
              <li key={product.id}>
                <button type="button" disabled={disabled} onClick={() => addProduct(product)}>
                  <span>
                    <strong>{product.name}</strong>
                    <span className="sub">{product.sku}</span>
                  </span>
                  <span className="muted">
                    {!product.isActive ? 'Inactive' : product.stockQuantity <= 0 ? 'Out of stock' : `${product.stockQuantity} in stock`}
                    {' · '}
                    {formatMoney(product.price)}
                  </span>
                </button>
              </li>
            )
          })}
        </ul>

        {lines.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Product</th>
                  <th className="num">Price</th>
                  <th className="num">Qty</th>
                  <th className="num">Line total</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {lines.map((line) => (
                  <tr key={line.product.id}>
                    <td data-label="Product">{line.product.name}</td>
                    <td data-label="Price" className="num">{formatMoney(line.product.price)}</td>
                    <td data-label="Qty" className="num">
                      <input
                        className="qty"
                        type="number"
                        min={1}
                        max={line.product.stockQuantity}
                        aria-label={`Quantity of ${line.product.name}`}
                        value={line.quantity}
                        onChange={(e) => setQuantity(line.product.id, e.target.value)}
                      />
                    </td>
                    <td data-label="Line total" className="num">{formatMoney(line.product.price * line.quantity)}</td>
                    <td data-label="Actions" className="actions-col">
                      <button
                        type="button"
                        className="icon-button danger"
                        aria-label={`Remove ${line.product.name}`}
                        onClick={() => setLines((current) => current.filter((l) => l.product.id !== line.product.id))}
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3} className="num">
                    <strong>Total</strong>
                  </td>
                  <td className="num">
                    <strong>{formatMoney(total)}</strong>
                  </td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
        )}

        {error && <p className="form-error" role="alert">{error}</p>}

        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Placing…' : 'Place order'}</button>
        </div>
      </form>
    </Modal>
  )
}
