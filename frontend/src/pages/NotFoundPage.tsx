import { Link } from 'react-router-dom'
import { EmptyState } from '../components/States'

export default function NotFoundPage() {
  return (
    <EmptyState title="Page not found">
      <Link to="/" className="link">
        Back to the dashboard
      </Link>
    </EmptyState>
  )
}
