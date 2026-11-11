import { useCallback, useState, type FormEvent } from 'react'
import { Check, UserPlus, UserRoundCheck, UserRoundX, X } from 'lucide-react'
import { usersApi } from '../api/endpoints'
import { errorMessage } from '../api/errors'
import type { CreateUserInput, ManagedUser, UserRole } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { Field } from '../components/Field'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { Pagination } from '../components/Pagination'
import { useQuery } from '../hooks/useQuery'
import { getPasswordRules } from '../lib/passwordRules'
import { useToast } from '../components/useToast'

const PAGE_SIZE = 20

function toUserRole(value: string): UserRole {
  return value === 'Admin' ? 'Admin' : 'Staff'
}

export default function TeamPage() {
  const { session } = useAuth()
  const { notify } = useToast()
  const [page, setPage] = useState(1)
  const fetchPage = useCallback(
    () => usersApi.list({ pageNumber: page, pageSize: PAGE_SIZE }),
    [page],
  )
  const query = useQuery(fetchPage)
  const [memberOverrides, setMemberOverrides] = useState<Record<string, ManagedUser>>({})
  const members = (query.data?.items ?? []).map((member) => memberOverrides[member.id] ?? member)
  const [pendingIds, setPendingIds] = useState<Set<string>>(() => new Set())
  const [addOpen, setAddOpen] = useState(false)

  function setPending(id: string, pending: boolean) {
    setPendingIds((current) => {
      const next = new Set(current)
      if (pending) next.add(id)
      else next.delete(id)
      return next
    })
  }

  async function changeRole(member: ManagedUser, role: UserRole) {
    if (member.roles.length === 1 && member.roles[0] === role) return
    setPending(member.id, true)
    setMemberOverrides((current) => ({
      ...current,
      [member.id]: { ...member, roles: [role] },
    }))
    try {
      const updated = await usersApi.updateRole(member.id, role)
      setMemberOverrides((current) => ({ ...current, [member.id]: updated }))
      notify(`Updated ${member.fullName}'s role to ${role}.`)
    } catch (error) {
      setMemberOverrides((current) => {
        const next = { ...current }
        delete next[member.id]
        return next
      })
      notify(errorMessage(error), 'error')
    } finally {
      setPending(member.id, false)
    }
  }

  async function changeActive(member: ManagedUser) {
    const isActive = !member.isActive
    setPending(member.id, true)
    setMemberOverrides((current) => ({
      ...current,
      [member.id]: { ...member, isActive },
    }))
    try {
      const updated = await usersApi.updateActive(member.id, isActive)
      setMemberOverrides((current) => ({ ...current, [member.id]: updated }))
      notify(isActive
        ? `Reactivated ${member.fullName} and cleared any lockout.`
        : `Deactivated ${member.fullName}. Their existing sessions have been revoked.`)
    } catch (error) {
      setMemberOverrides((current) => {
        const next = { ...current }
        delete next[member.id]
        return next
      })
      notify(errorMessage(error), 'error')
    } finally {
      setPending(member.id, false)
    }
  }

  async function createMember(input: CreateUserInput) {
    const created = await usersApi.create(input)
    setAddOpen(false)
    notify(`Added ${created.fullName} to the team as ${input.role}.`)
    query.reload()
  }

  const currentEmail = session?.email.toLowerCase()
  const isInitialLoading = query.loading && !query.data

  return (
    <>
      <PageHeader
        title="Team"
        subtitle="Manage who can access InventoryPro and what they can do."
        actions={
          <button type="button" className="btn btn-primary" onClick={() => setAddOpen(true)}>
            <UserPlus size={16} /> Add member
          </button>
        }
      />

      {query.error && <ErrorBanner message={query.error} onRetry={() => query.reload()} />}

      <section className="panel" aria-label="Team members">
        {isInitialLoading ? <LoadingBlock label="Loading team members…" /> : members.length === 0 ? (
          <EmptyState title="No team members found">
            <button type="button" className="btn btn-primary" onClick={() => setAddOpen(true)}><UserPlus size={16} /> Add first member</button>
          </EmptyState>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th scope="col">Member</th>
                  <th scope="col">Role</th>
                  <th scope="col">Status</th>
                  <th scope="col">Joined</th>
                  <th scope="col" className="actions-col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {members.map((member) => {
                  const isSelf = member.email.toLowerCase() === currentEmail
                  const pending = pendingIds.has(member.id)
                  const role = member.roles[0] ?? 'Staff'
                  return (
                    <tr key={member.id} data-testid={`team-member-${member.id}`}>
                      <td data-label="Member">
                        <strong>{member.fullName}</strong>
                        <span className="sub">{member.email}</span>
                      </td>
                      <td data-label="Role">
                        <label className="visually-hidden" htmlFor={`role-${member.id}`}>
                          Role for {member.fullName}
                        </label>
                        <select
                          id={`role-${member.id}`}
                          className="team-role-select"
                          aria-label={`Role for ${member.fullName}`}
                          value={role}
                          disabled={isSelf || pending}
                          onChange={(event) => void changeRole(member, toUserRole(event.target.value))}
                        >
                          <option value="Staff">Staff</option>
                          <option value="Admin">Admin</option>
                        </select>
                        {isSelf && <span className="sub">Your role is protected</span>}
                      </td>
                      <td data-label="Status">
                        <span className={`badge ${member.isActive ? 'badge-green' : 'badge-grey'}`}>
                          {member.isActive ? 'Active' : 'Inactive'}
                        </span>
                        {member.isLockedOut && <span className="sub">Sign-in locked</span>}
                      </td>
                      <td data-label="Joined">{new Date(member.createdAt).toLocaleDateString()}</td>
                      <td data-label="Actions" className="actions-col">
                        <button
                          type="button"
                          className={member.isActive ? 'btn btn-ghost team-action' : 'btn btn-primary team-action'}
                          aria-label={`${member.isActive ? 'Deactivate' : 'Reactivate'} ${member.fullName}`}
                          disabled={isSelf || pending}
                          onClick={() => void changeActive(member)}
                        >
                          {member.isActive
                            ? <><UserRoundX size={16} /> Deactivate</>
                            : <><UserRoundCheck size={16} /> Reactivate</>}
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
        {query.data && (
          <Pagination
            page={query.data.pageNumber}
            totalPages={query.data.totalPages}
            totalCount={query.data.totalCount}
            onChange={setPage}
          />
        )}
      </section>

      {addOpen && (
        <AddMemberModal onClose={() => setAddOpen(false)} onCreate={createMember} />
      )}
    </>
  )
}

function AddMemberModal({
  onClose,
  onCreate,
}: {
  onClose: () => void
  onCreate: (input: CreateUserInput) => Promise<void>
}) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<UserRole>('Staff')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const rules = getPasswordRules(password)
  const passwordValid = rules.every((rule) => rule.valid)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!passwordValid || saving) return
    setSaving(true)
    setError(null)
    try {
      await onCreate({ fullName: fullName.trim(), email: email.trim(), password, role })
    } catch (submitError) {
      setError(errorMessage(submitError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title="Add team member" onClose={onClose}>
      <form className="form" onSubmit={(event) => void submit(event)}>
        <Field label="Full name" htmlFor="new-member-name">
          <input
            id="new-member-name"
            autoFocus
            autoComplete="name"
            required
            maxLength={150}
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
          />
        </Field>
        <Field label="Email address" htmlFor="new-member-email">
          <input
            id="new-member-email"
            type="email"
            autoComplete="email"
            required
            maxLength={256}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </Field>
        <Field label="Temporary password" htmlFor="new-member-password">
          <input
            id="new-member-password"
            type="password"
            autoComplete="new-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            aria-describedby="password-rules"
          />
        </Field>
        <ul className="password-rules" id="password-rules" aria-live="polite">
          {rules.map((rule) => (
            <li key={rule.id} className={rule.valid ? 'password-rule valid' : 'password-rule'}>
              {rule.valid ? <Check size={15} aria-hidden="true" /> : <X size={15} aria-hidden="true" />}
              {rule.label}
            </li>
          ))}
        </ul>
        <Field label="Role" htmlFor="new-member-role">
          <select
            id="new-member-role"
            value={role}
            onChange={(event) => setRole(toUserRole(event.target.value))}
          >
            <option value="Staff">Staff</option>
            <option value="Admin">Admin</option>
          </select>
        </Field>
        {error && <p className="form-error" role="alert">{error}</p>}
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={saving}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={!passwordValid || saving}>
            {saving ? 'Adding…' : 'Add member'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
