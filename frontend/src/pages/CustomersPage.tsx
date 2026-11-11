import { useCallback, useState, type FormEvent } from 'react'
import { Plus } from 'lucide-react'
import { errorMessage } from '../api/errors'
import { customersApi } from '../api/endpoints'
import { Field } from '../components/Field'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'
import { Pagination } from '../components/Pagination'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { useQuery } from '../hooks/useQuery'
import { formatDate } from '../lib/format'
import { useToast } from '../components/useToast'

const PAGE_SIZE = 10

export default function CustomersPage() {
  const { notify } = useToast()
  const [page, setPage] = useState(1)
  const [adding, setAdding] = useState(false)

  const customers = useQuery(useCallback(() => customersApi.list({ pageNumber: page, pageSize: PAGE_SIZE }), [page]))
  const data = customers.data

  return (
    <>
      <PageHeader
        title="Customers"
        subtitle="People and businesses you sell to."
        actions={
          <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}>
            <Plus size={16} /> Add customer
          </button>
        }
      />

      {customers.error && <ErrorBanner message={customers.error} onRetry={customers.reload} />}

      <section className="panel">
        {!data && !customers.error && <LoadingBlock />}
        {data && data.items.length === 0 && (
          <EmptyState title="No customers yet">
            <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}><Plus size={16} /> Add first customer</button>
          </EmptyState>
        )}
        {data && data.items.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>Address</th>
                  <th>Added</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((customer) => (
                  <tr key={customer.id}>
                    <td data-label="Name">
                      <strong>{customer.fullName}</strong>
                    </td>
                    <td data-label="Email">{customer.email}</td>
                    <td data-label="Phone">{customer.phone ?? '—'}</td>
                    <td data-label="Address">{customer.address ?? '—'}</td>
                    <td data-label="Added">{formatDate(customer.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {data && <Pagination page={data.pageNumber} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />}
      </section>

      {adding && (
        <CustomerModal
          onClose={() => setAdding(false)}
          onSaved={() => {
            setAdding(false)
            notify('Customer added.')
            customers.reload()
          }}
        />
      )}
    </>
  )
}

function CustomerModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [address, setAddress] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (!fullName.trim()) return setError('Name is required.')
    if (!email.trim()) return setError('Email is required.')

    setBusy(true)
    try {
      await customersApi.create({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim() || null,
        address: address.trim() || null,
      })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal title="Add customer" onClose={onClose}>
      <form onSubmit={submit} className="form">
        <Field label="Full name" htmlFor="c-name">
          <input id="c-name" value={fullName} maxLength={150} onChange={(e) => setFullName(e.target.value)} autoFocus />
        </Field>
        <Field label="Email" htmlFor="c-email">
          <input id="c-email" type="email" value={email} maxLength={150} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <div className="form-row">
          <Field label="Phone" htmlFor="c-phone">
            <input id="c-phone" value={phone} maxLength={20} onChange={(e) => setPhone(e.target.value)} />
          </Field>
        </div>
        <Field label="Address" htmlFor="c-address">
          <textarea id="c-address" rows={2} value={address} maxLength={300} onChange={(e) => setAddress(e.target.value)} />
        </Field>

        {error && <p className="form-error" role="alert">{error}</p>}

        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save customer'}</button>
        </div>
      </form>
    </Modal>
  )
}
