import { ChevronLeft, ChevronRight } from 'lucide-react'

interface PaginationProps {
  page: number
  totalPages: number
  totalCount: number
  onChange: (page: number) => void
}

export function Pagination({ page, totalPages, totalCount, onChange }: PaginationProps) {
  if (totalCount === 0) return null

  return (
    <nav className="pagination" aria-label="Pagination">
      <span className="muted">
        Page {page} of {Math.max(totalPages, 1)} · {totalCount} total
      </span>
      <div className="pagination-buttons">
        <button type="button" className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => onChange(page - 1)}>
          <ChevronLeft size={16} /> Previous
        </button>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          disabled={page >= totalPages}
          onClick={() => onChange(page + 1)}
        >
          Next <ChevronRight size={16} />
        </button>
      </div>
    </nav>
  )
}
