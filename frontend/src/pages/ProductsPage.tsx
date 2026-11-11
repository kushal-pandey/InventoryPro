import { useCallback, useRef, useState, type FormEvent } from 'react'
import { ArrowDown, ArrowUp, ArrowUpDown, Pencil, Plus, Search, SlidersHorizontal, Trash2 } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'
import { errorMessage } from '../api/errors'
import { categoriesApi, productsApi } from '../api/endpoints'
import type { Category, Product, ProductListParams } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { ActiveBadge } from '../components/Badges'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Field } from '../components/Field'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'
import { Pagination } from '../components/Pagination'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useQuery } from '../hooks/useQuery'
import { useSearchShortcut } from '../hooks/useSearchShortcut'
import { formatMoney, formatNumber } from '../lib/format'
import { readProductListUrl, updateListUrl } from '../lib/listUrlState'
import { useToast } from '../components/useToast'

const PAGE_SIZE = 10

type Dialog =
  | { kind: 'create' }
  | { kind: 'edit'; product: Product }
  | { kind: 'stock'; product: Product }
  | { kind: 'delete'; product: Product }
  | null

export default function ProductsPage() {
  const { isAdmin } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const { page, search, categoryId, sort, lowStockOnly } = readProductListUrl(searchParams)
  const { notify } = useToast()
  const searchRef = useRef<HTMLInputElement>(null)
  useSearchShortcut(searchRef)
  const [dialog, setDialog] = useState<Dialog>(null)

  const debouncedSearch = useDebouncedValue(search.trim())
  const [sortBy, direction] = sort.split(':')

  function updateUrl(updates: Record<string, string | number | boolean | null>, resetPage = false, replace = false) {
    setSearchParams(updateListUrl(searchParams, updates, resetPage), { replace })
  }

  const products = useQuery(
    useCallback(
      () =>
        productsApi.list({
          pageNumber: page,
          pageSize: PAGE_SIZE,
          search: debouncedSearch || undefined,
          categoryId: categoryId || undefined,
          lowStockOnly: lowStockOnly || undefined,
          sortBy: sortBy as ProductListParams['sortBy'],
          sortDescending: direction === 'desc',
        }),
      [page, debouncedSearch, categoryId, lowStockOnly, sortBy, direction],
    ),
  )
  const categories = useQuery(useCallback(() => categoriesApi.list({ pageSize: 100 }), []))

  const data = products.data

  function done(message: string) {
    setDialog(null)
    notify(message)
    products.reload()
  }

  return (
    <>
      <PageHeader
        title="Products"
        subtitle="Everything you stock, with live quantities."
        actions={
          isAdmin && (
            <button type="button" className="btn btn-primary" onClick={() => setDialog({ kind: 'create' })}>
              <Plus size={16} /> Add product
            </button>
          )
        }
      />

      <section className="panel panel-pad">
        <div className="toolbar">
          <div className="search">
            <Search size={16} aria-hidden="true" />
            <input
              type="search"
              placeholder="Search by name or SKU"
              aria-label="Search products"
              ref={searchRef}
              value={search}
              onChange={(event) => updateUrl({ q: event.target.value }, true, true)}
            />
          </div>

          <select
            aria-label="Filter by category"
            value={categoryId}
            onChange={(event) => updateUrl({ category: Number(event.target.value) || null }, true)}
          >
            <option value={0}>All categories</option>
            {categories.data?.items.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>

          <label className="check">
            <input
              type="checkbox"
              checked={lowStockOnly}
              onChange={(event) => updateUrl({ low: event.target.checked ? 'true' : null }, true)}
            />
            Low stock only
          </label>
        </div>
      </section>

      {products.error && <ErrorBanner message={products.error} onRetry={products.reload} />}

      <section className="panel">
        {!data && !products.error && <LoadingBlock />}
        {data && data.items.length === 0 && (
          <EmptyState title="No products found">
            <button type="button" className="btn btn-ghost" onClick={() => setSearchParams(new URLSearchParams())}>
              Clear search and filters
            </button>
          </EmptyState>
        )}
        {data && data.items.length > 0 && (
          <div className={products.loading ? 'table-wrap is-loading' : 'table-wrap'}>
            <table>
              <thead>
                <tr>
                  <SortableHeader label="Product" field="name" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} />
                  <th>Category</th>
                  <SortableHeader label="Price" field="price" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} numeric />
                  <SortableHeader label="In stock" field="stock" sort={sort} onSort={(next) => updateUrl({ sort: next }, true)} numeric />
                  <th className="num">Reorder at</th>
                  <th>Status</th>
                  {isAdmin && <th className="actions-col">Actions</th>}
                </tr>
              </thead>
              <tbody>
                {data.items.map((product) => (
                  <tr key={product.id}>
                    <td data-label="Product">
                      <strong>{product.name}</strong>
                      <span className="sub">{product.sku}</span>
                    </td>
                    <td data-label="Category">{product.categoryName}</td>
                    <td data-label="Price" className="num">{formatMoney(product.price)}</td>
                    <td data-label="In stock" className="num">
                      <span className={product.lowStock ? 'stock stock-low' : 'stock'}>
                        {formatNumber(product.stockQuantity)}
                      </span>
                    </td>
                    <td data-label="Reorder at" className="num">{formatNumber(product.reorderLevel)}</td>
                    <td data-label="Status">
                      <ActiveBadge active={product.isActive} />
                    </td>
                    {isAdmin && (
                      <td data-label="Actions" className="actions-col">
                        <button type="button" className="icon-button" aria-label={`Adjust stock for ${product.name}`} title="Adjust stock" onClick={() => setDialog({ kind: 'stock', product })}>
                          <SlidersHorizontal size={16} />
                        </button>
                        <button type="button" className="icon-button" aria-label={`Edit ${product.name}`} title="Edit" onClick={() => setDialog({ kind: 'edit', product })}>
                          <Pencil size={16} />
                        </button>
                        <button type="button" className="icon-button danger" aria-label={`Delete ${product.name}`} title="Delete" onClick={() => setDialog({ kind: 'delete', product })}>
                          <Trash2 size={16} />
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {data && <Pagination page={data.pageNumber} totalPages={data.totalPages} totalCount={data.totalCount} onChange={(next) => updateUrl({ page: next })} />}
      </section>

      {dialog?.kind === 'create' && (
        <ProductFormModal
          categories={categories.data?.items ?? []}
          onClose={() => setDialog(null)}
          onSaved={() => done('Product created.')}
        />
      )}
      {dialog?.kind === 'edit' && (
        <ProductFormModal
          product={dialog.product}
          categories={categories.data?.items ?? []}
          onClose={() => setDialog(null)}
          onSaved={() => done('Product updated.')}
        />
      )}
      {dialog?.kind === 'stock' && (
        <StockModal product={dialog.product} onClose={() => setDialog(null)} onSaved={() => done('Stock updated.')} />
      )}
      {dialog?.kind === 'delete' && (
        <ConfirmDialog
          title="Delete product"
          message={`Delete "${dialog.product.name}"? Products that already appear on orders are deactivated instead of removed, so order history stays intact.`}
          confirmLabel="Delete"
          danger
          onClose={() => setDialog(null)}
          onConfirm={async () => {
            await productsApi.remove(dialog.product.id)
            if (data && data.items.length === 1 && page > 1) updateUrl({ page: page - 1 })
            done('Product deleted.')
          }}
        />
      )}
    </>
  )
}

function SortableHeader({
  label,
  field,
  sort,
  onSort,
  numeric = false,
}: {
  label: string
  field: 'name' | 'price' | 'stock'
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

function ProductFormModal({
  product,
  categories,
  onClose,
  onSaved,
}: {
  product?: Product
  categories: Category[]
  onClose: () => void
  onSaved: () => void
}) {
  const editing = product !== undefined
  const [name, setName] = useState(product?.name ?? '')
  const [sku, setSku] = useState(product?.sku ?? '')
  const [description, setDescription] = useState(product?.description ?? '')
  const [categoryId, setCategoryId] = useState(product?.categoryId ?? categories[0]?.id ?? 0)
  const [price, setPrice] = useState(product ? String(product.price) : '')
  const [stock, setStock] = useState('0')
  const [reorderLevel, setReorderLevel] = useState(product ? String(product.reorderLevel) : '5')
  const [isActive, setIsActive] = useState(product?.isActive ?? true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const priceValue = Number(price)
    const reorderValue = Number.parseInt(reorderLevel, 10)
    const stockValue = Number.parseInt(stock, 10)

    if (!name.trim()) return setError('Name is required.')
    if (!editing && !sku.trim()) return setError('SKU is required.')
    if (!categoryId) return setError('Choose a category (create one first if the list is empty).')
    if (!Number.isFinite(priceValue) || priceValue <= 0) return setError('Price must be greater than zero.')
    if (Number.isNaN(reorderValue) || reorderValue < 0) return setError('Reorder level cannot be negative.')
    if (!editing && (Number.isNaN(stockValue) || stockValue < 0)) return setError('Starting stock cannot be negative.')

    setBusy(true)
    try {
      if (editing) {
        await productsApi.update(product.id, {
          name: name.trim(),
          description: description.trim() || null,
          price: priceValue,
          reorderLevel: reorderValue,
          categoryId,
          isActive,
        })
      } else {
        await productsApi.create({
          name: name.trim(),
          sku: sku.trim(),
          description: description.trim() || null,
          price: priceValue,
          stockQuantity: stockValue,
          reorderLevel: reorderValue,
          categoryId,
        })
      }
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal title={editing ? 'Edit product' : 'Add product'} onClose={onClose}>
      <form onSubmit={submit} className="form">
        <Field label="Name" htmlFor="p-name">
          <input id="p-name" value={name} maxLength={150} onChange={(e) => setName(e.target.value)} autoFocus />
        </Field>

        <div className="form-row">
          <Field label="SKU" htmlFor="p-sku" hint={editing ? 'SKU cannot be changed.' : undefined}>
            <input id="p-sku" value={sku} maxLength={50} disabled={editing} onChange={(e) => setSku(e.target.value)} />
          </Field>
          <Field label="Category" htmlFor="p-category">
            <select id="p-category" value={categoryId} onChange={(e) => setCategoryId(Number(e.target.value))}>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="form-row">
          <Field label="Price (₹)" htmlFor="p-price">
            <input id="p-price" type="number" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />
          </Field>
          {!editing && (
            <Field label="Starting stock" htmlFor="p-stock">
              <input id="p-stock" type="number" min="0" step="1" value={stock} onChange={(e) => setStock(e.target.value)} />
            </Field>
          )}
          <Field label="Reorder level" htmlFor="p-reorder" hint="Minimum before it is flagged.">
            <input id="p-reorder" type="number" min="0" step="1" value={reorderLevel} onChange={(e) => setReorderLevel(e.target.value)} />
          </Field>
        </div>

        <Field label="Description" htmlFor="p-desc">
          <textarea id="p-desc" rows={2} value={description} maxLength={500} onChange={(e) => setDescription(e.target.value)} />
        </Field>

        {editing && (
          <label className="check">
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            Active (can be ordered)
          </label>
        )}

        {error && <p className="form-error" role="alert">{error}</p>}

        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        </div>
      </form>
    </Modal>
  )
}

function StockModal({ product, onClose, onSaved }: { product: Product; onClose: () => void; onSaved: () => void }) {
  const [change, setChange] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const delta = Number.parseInt(change, 10)
  const valid = !Number.isNaN(delta) && delta !== 0

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (!valid) return setError('Enter a whole number other than zero (negative removes stock).')
    if (!reason.trim()) return setError('Give a reason so the change can be audited.')

    setBusy(true)
    try {
      await productsApi.adjustStock(product.id, { quantityChange: delta, reason: reason.trim() })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal title={`Adjust stock: ${product.name}`} onClose={onClose}>
      <form onSubmit={submit} className="form">
        <p className="modal-text">
          Currently <strong>{formatNumber(product.stockQuantity)}</strong> in stock.
          {valid && (
            <>
              {' '}After this change: <strong>{formatNumber(product.stockQuantity + delta)}</strong>.
            </>
          )}
        </p>

        <Field label="Change (+ to add, − to remove)" htmlFor="s-change">
          <input id="s-change" type="number" step="1" value={change} onChange={(e) => setChange(e.target.value)} autoFocus />
        </Field>
        <Field label="Reason" htmlFor="s-reason" hint="For example: supplier delivery, damaged, stock count.">
          <input id="s-reason" value={reason} maxLength={200} onChange={(e) => setReason(e.target.value)} />
        </Field>

        {error && <p className="form-error" role="alert">{error}</p>}

        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Apply change'}</button>
        </div>
      </form>
    </Modal>
  )
}
