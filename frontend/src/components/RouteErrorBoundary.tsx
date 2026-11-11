import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Link } from 'react-router-dom'

interface State {
  error: Error | null
}

export class RouteErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('A route failed to render.', error, info)
  }

  render() {
    if (!this.state.error) return this.props.children

    return (
      <section className="panel panel-pad route-error" role="alert">
        <h1>This page could not be displayed</h1>
        <p>Something went wrong while loading this screen. You can try again or return to the dashboard.</p>
        <div className="page-actions">
          <button type="button" className="btn btn-ghost" onClick={() => window.location.reload()}>Reload page</button>
          <Link className="btn btn-primary" to="/">Go to dashboard</Link>
        </div>
      </section>
    )
  }
}
