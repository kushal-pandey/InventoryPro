import type { ReactNode } from 'react'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { usersApi } from '../api/endpoints'
import type { ManagedUser, PagedResult } from '../api/types'
import { AuthContext, type AuthContextValue } from '../auth/AuthContext'
import { ToastProvider } from '../components/ToastProvider'
import TeamPage from './TeamPage'

vi.mock('../api/endpoints', () => ({
  usersApi: {
    list: vi.fn(),
    create: vi.fn(),
    updateRole: vi.fn(),
    updateActive: vi.fn(),
  },
}))

vi.mock('../components/Modal', () => ({
  Modal: ({
    title,
    onClose,
    children,
  }: {
    title: string
    onClose: () => void
    children: ReactNode
  }) => (
    <section role="dialog" aria-label={title}>
      <button type="button" aria-label="Close dialog" onClick={onClose}>Close</button>
      {children}
    </section>
  ),
}))

const admin: ManagedUser = {
  id: 'admin-id',
  fullName: 'Inventory Admin',
  email: 'admin@example.test',
  roles: ['Admin'],
  isActive: true,
  isLockedOut: false,
  createdAt: '2026-01-01T00:00:00Z',
}

const staff: ManagedUser = {
  id: 'staff-id',
  fullName: 'Staff User',
  email: 'staff@example.test',
  roles: ['Staff'],
  isActive: true,
  isLockedOut: false,
  createdAt: '2026-01-02T00:00:00Z',
}

const page = (items: ManagedUser[]): PagedResult<ManagedUser> => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
  hasPrevious: false,
  hasNext: false,
})

const authValue: AuthContextValue = {
  session: {
    token: 'token',
    expiresAt: '2099-01-01T00:00:00Z',
    email: admin.email,
    fullName: admin.fullName,
    roles: ['Admin'],
  },
  isAdmin: true,
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

function renderTeamPage() {
  return render(
    <AuthContext.Provider value={authValue}>
      <ToastProvider><TeamPage /></ToastProvider>
    </AuthContext.Provider>,
  )
}

describe('TeamPage', () => {
  it('lists members and protects the current administrator controls', async () => {
    vi.mocked(usersApi.list).mockResolvedValue(page([admin, staff]))
    renderTeamPage()

    expect(await screen.findByText('Staff User')).toBeTruthy()
    expect((screen.getByRole('combobox', { name: 'Role for Inventory Admin' }) as HTMLSelectElement).disabled)
      .toBe(true)
    expect((screen.getByRole('button', { name: 'Deactivate Inventory Admin' }) as HTMLButtonElement).disabled)
      .toBe(true)
    expect((screen.getByRole('combobox', { name: 'Role for Staff User' }) as HTMLSelectElement).disabled)
      .toBe(false)
  })

  it('shows live password feedback and creates a member only after every rule passes', async () => {
    vi.mocked(usersApi.list).mockResolvedValue(page([admin]))
    vi.mocked(usersApi.create).mockResolvedValue(staff)
    renderTeamPage()
    await screen.findByText('Inventory Admin')

    fireEvent.click(screen.getByRole('button', { name: /add member/i }))
    const dialog = within(screen.getByRole('dialog', { name: 'Add team member' }))
    const submit = dialog.getByRole('button', { name: 'Add member' }) as HTMLButtonElement
    expect(submit.disabled).toBe(true)

    fireEvent.change(dialog.getByLabelText('Full name'), { target: { value: 'New Staff' } })
    fireEvent.change(dialog.getByLabelText('Email address'), { target: { value: 'new-staff@example.test' } })
    fireEvent.change(dialog.getByLabelText('Temporary password'), { target: { value: 'InventoryPro!2026' } })
    fireEvent.change(dialog.getByLabelText('Role'), { target: { value: 'Admin' } })

    expect(dialog.getByText('At least 12 characters').className).toContain('valid')
    expect(submit.disabled).toBe(false)
    fireEvent.click(submit)

    await waitFor(() => expect(usersApi.create).toHaveBeenCalledWith({
      fullName: 'New Staff',
      email: 'new-staff@example.test',
      password: 'InventoryPro!2026',
      role: 'Admin',
    }))
    expect(await screen.findByText('Added Staff User to the team as Admin.')).toBeTruthy()
  })

  it('optimistically changes a role and rolls it back when the API fails', async () => {
    vi.mocked(usersApi.list).mockResolvedValue(page([admin, staff]))
    vi.mocked(usersApi.updateRole).mockRejectedValue(new Error('Role update failed'))
    renderTeamPage()
    await screen.findByText('Staff User')

    const roleSelect = screen.getByRole('combobox', { name: 'Role for Staff User' }) as HTMLSelectElement
    fireEvent.change(roleSelect, { target: { value: 'Admin' } })
    expect(roleSelect.value).toBe('Admin')

    expect((await screen.findByRole('alert')).textContent).toContain('Role update failed')
    await waitFor(() => expect(roleSelect.value).toBe('Staff'))
  })

  it('optimistically deactivates an account and restores it when the API fails', async () => {
    vi.mocked(usersApi.list).mockResolvedValue(page([admin, staff]))
    vi.mocked(usersApi.updateActive).mockRejectedValue(new Error('Deactivate failed'))
    renderTeamPage()
    await screen.findByText('Staff User')

    fireEvent.click(screen.getByRole('button', { name: 'Deactivate Staff User' }))
    const memberRow = within(screen.getByTestId(`team-member-${staff.id}`))
    expect(memberRow.getByText('Inactive')).toBeTruthy()

    expect((await screen.findByRole('alert')).textContent).toContain('Deactivate failed')
    expect(memberRow.getByText('Active')).toBeTruthy()
    expect(usersApi.updateActive).toHaveBeenCalledWith(staff.id, false)
  })
})
