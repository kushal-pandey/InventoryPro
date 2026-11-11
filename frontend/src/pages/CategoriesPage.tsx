import { useCallback, useState, type FormEvent } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import { errorMessage } from '../api/errors'
import { categoriesApi } from '../api/endpoints'
import type { Category } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Field } from '../components/Field'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'
import { Pagination } from '../components/Pagination'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { useQuery } from '../hooks/useQuery'
import { useToast } from '../components/useToast'

const PAGE_SIZE = 20

export default function CategoriesPage() {
  const { isAdmin } = useAuth()
  const { notify } = useToast()
  const [page, setPage] = useState(1)
  const [adding, setAdding] = useState(false)
  const [deleting, setDeleting] = useState<Category | null>(null)

  const categories = useQuery(useCallback(() => categoriesApi.list({ pageNumber: page, pageSize: PAGE_SIZE }), [page]))
  const data = categories.data

  return (
    <>
      <PageHeader
        title="Categories"
        subtitle="Group products so they are easy to find and report on."
        actions={
          isAdmin && (
            <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}>
              <Plus size={16} /> Add category
            </button>
          )
        }
      />

      {categories.error && <ErrorBanner message={categories.error} onRetry={categories.reload} />}

      <section className="panel">
        {!data && !categories.error && <LoadingBlock />}
        {data && data.items.length === 0 && (
          <EmptyState title="No categories yet">
            {isAdmin
              ? <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}><Plus size={16} /> Add first category</button>
              : 'Products need a category, so ask an administrator to add one.'}
          </EmptyState>
        )}
        {data && data.items.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Description</th>
                  <th className="num">Products</th>
                  {isAdmin && <th className="actions-col">Actions</th>}
                </tr>
              </thead>
              <tbody>
                {data.items.map((category) => (
                  <tr key={category.id}>
                    <td data-label="Name">
                      <strong>{category.name}</strong>
                    </td>
                    <td data-label="Description">{category.description ?? '—'}</td>
                    <td data-label="Products" className="num">{category.productCount}</td>
                    {isAdmin && (
                      <td data-label="Actions" className="actions-col">
                        <button type="button" className="icon-button danger" aria-label={`Delete ${category.name}`} onClick={() => setDeleting(category)}>
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
        {data && <Pagination page={data.pageNumber} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />}
      </section>

      {adding && (
        <CategoryModal
          onClose={() => setAdding(false)}
          onSaved={() => {
            setAdding(false)
            notify('Category added.')
            categories.reload()
          }}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title="Delete category"
          message={`Delete "${deleting.name}"? A category that still has products cannot be deleted.`}
          confirmLabel="Delete"
          danger
          onClose={() => setDeleting(null)}
          onConfirm={async () => {
            await categoriesApi.remove(deleting.id)
            setDeleting(null)
            notify('Category deleted.')
            categories.reload()
          }}
        />
      )}
    </>
  )
}

function CategoryModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (!name.trim()) return setError('Name is required.')

    setBusy(true)
    try {
      await categoriesApi.create({ name: name.trim(), description: description.trim() || null })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal title="Add category" onClose={onClose}>
      <form onSubmit={submit} className="form">
        <Field label="Name" htmlFor="cat-name">
          <input id="cat-name" value={name} maxLength={100} onChange={(e) => setName(e.target.value)} autoFocus />
        </Field>
        <Field label="Description" htmlFor="cat-desc">
          <textarea id="cat-desc" rows={2} value={description} maxLength={300} onChange={(e) => setDescription(e.target.value)} />
        </Field>

        {error && <p className="form-error" role="alert">{error}</p>}

        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save category'}</button>
        </div>
      </form>
    </Modal>
  )
}
