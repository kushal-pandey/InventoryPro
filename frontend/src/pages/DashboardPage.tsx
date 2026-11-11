import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { AlertTriangle, Boxes, Hourglass, IndianRupee } from 'lucide-react'
import { insightsApi, ordersApi } from '../api/endpoints'
import { HealthBadge, OrderStatusBadge } from '../components/Badges'
import { MetricCard } from '../components/MetricCard'
import { PageHeader } from '../components/PageHeader'
import { EmptyState, ErrorBanner, LoadingBlock } from '../components/States'
import { useQuery } from '../hooks/useQuery'
import { formatCompactMoney, formatDateTime, formatMoney, formatNumber, formatShortDate } from '../lib/format'

export default function DashboardPage() {
  const summary = useQuery(useCallback(() => insightsApi.summary(), []))
  const [rangeDays, setRangeDays] = useState(30)
  const trend = useQuery(useCallback(() => insightsApi.salesTrend(rangeDays), [rangeDays]))
  const reorder = useQuery(useCallback(() => insightsApi.reorder({ limit: 5 }), []))
  const orders = useQuery(useCallback(() => ordersApi.list({ pageNumber: 1, pageSize: 5 }), []))
  const reloadSummary = summary.reload
  const reloadTrend = trend.reload
  const reloadReorder = reorder.reload
  const reloadOrders = orders.reload
  const refreshAll = useCallback(() => {
    reloadSummary()
    reloadTrend()
    reloadReorder()
    reloadOrders()
  }, [reloadSummary, reloadTrend, reloadReorder, reloadOrders])
  const lastUpdated = useMemo(() => {
    const times = [summary.updatedAt, trend.updatedAt, reorder.updatedAt, orders.updatedAt]
    return times.every((time): time is number => time !== undefined) ? Math.min(...times) : undefined
  }, [summary.updatedAt, trend.updatedAt, reorder.updatedAt, orders.updatedAt])
  const salesTotals = useMemo(
    () => (trend.data ?? []).reduce((total, point) => ({
      revenue: total.revenue + point.revenue,
      orders: total.orders + point.orders,
    }), { revenue: 0, orders: 0 }),
    [trend.data],
  )

  useEffect(() => {
    const interval = window.setInterval(refreshAll, 60_000)
    window.addEventListener('focus', refreshAll)
    return () => {
      window.clearInterval(interval)
      window.removeEventListener('focus', refreshAll)
    }
  }, [refreshAll])

  const s = summary.data

  return (
    <>
      <PageHeader
        title="Dashboard"
        subtitle="What needs your attention today."
        actions={
          <label className="range-control">
            <span>Sales range</span>
            <select aria-label="Sales date range" value={rangeDays} onChange={(event) => setRangeDays(Number(event.target.value))}>
              <option value={7}>7 days</option>
              <option value={30}>30 days</option>
              <option value={90}>90 days</option>
            </select>
          </label>
        }
      />
      <p className="last-updated muted" aria-live="polite">
        {lastUpdated === undefined ? 'Updating dashboard…' : `Last updated ${formatDateTime(new Date(lastUpdated).toISOString())}`}
      </p>

      {summary.error && <ErrorBanner message={summary.error} onRetry={summary.reload} />}

      {!s && !summary.error ? (
        <LoadingBlock />
      ) : (
        s && (
          <div className="metric-grid">
            <MetricCard
              label="Stock value"
              value={formatMoney(s.inventoryValue)}
              hint={`${formatNumber(s.totalUnitsOnHand)} units · ${s.activeProducts} products (at selling price)`}
              icon={<Boxes size={20} />}
            />
            <Link to="/reorder?status=needs-reorder" className="metric-link" aria-label={`${s.needsReorderCount} products need reordering; open reorder planner`}>
              <MetricCard
                label="Needs reordering"
                value={String(s.needsReorderCount)}
                hint={`${s.outOfStockCount} already out of stock`}
                tone={s.needsReorderCount > 0 ? 'danger' : 'default'}
                icon={<AlertTriangle size={20} />}
              />
            </Link>
            <MetricCard
              label="Cash tied up in slow stock"
              value={formatMoney(s.slowStockValue)}
              hint={`${s.deadStockCount} not selling · ${s.overstockedCount} overstocked`}
              tone={s.slowStockValue > 0 ? 'warning' : 'default'}
              icon={<Hourglass size={20} />}
            />
            <MetricCard
              label={`Revenue, last ${rangeDays} days`}
              value={trend.data ? formatMoney(salesTotals.revenue) : '—'}
              hint={trend.data ? `${salesTotals.orders} orders (cancelled excluded)` : 'Sales history is loading'}
              icon={<IndianRupee size={20} />}
            />
          </div>
        )
      )}

      {s && (
        <div className="health-links" role="group" aria-label="Stock health filters">
          <Link to="/reorder?status=OutOfStock" className="health-link"><span>Out of stock</span><strong>{s.outOfStockCount}</strong></Link>
          <Link to="/reorder?status=needs-reorder" className="health-link"><span>Needs reorder</span><strong>{s.needsReorderCount}</strong></Link>
          <Link to="/reorder?status=Overstocked" className="health-link"><span>Overstocked</span><strong>{s.overstockedCount}</strong></Link>
          <Link to="/reorder?status=Dead" className="health-link"><span>Not selling</span><strong>{s.deadStockCount}</strong></Link>
        </div>
      )}

      <div className="grid-2">
        <section className="panel">
          <div className="panel-header">
            <h2>Sales, last {rangeDays} days</h2>
          </div>
          {trend.error && <ErrorBanner message={trend.error} onRetry={trend.reload} />}
          {!trend.data && !trend.error && <LoadingBlock />}
          {trend.data && (
            <div className="chart" role="img" aria-label={`Daily revenue for the last ${rangeDays} days`}>
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={trend.data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
                  <defs>
                    <linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="#7164ed" stopOpacity={0.35} />
                      <stop offset="100%" stopColor="#7164ed" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="#eceef5" vertical={false} />
                  <XAxis
                    dataKey="date"
                    tickFormatter={formatShortDate}
                    tick={{ fontSize: 12, fill: '#858ba3' }}
                    tickLine={false}
                    axisLine={false}
                    minTickGap={28}
                  />
                  <YAxis
                    tickFormatter={(value: number) => formatCompactMoney(value)}
                    tick={{ fontSize: 12, fill: '#858ba3' }}
                    tickLine={false}
                    axisLine={false}
                    width={64}
                  />
                  <Tooltip
                    labelFormatter={(label) => formatShortDate(String(label))}
                    formatter={(value) => [formatMoney(Number(value)), 'Revenue']}
                  />
                  <Area type="monotone" dataKey="revenue" stroke="#7164ed" strokeWidth={2} fill="url(#revenueFill)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="panel">
          <div className="panel-header">
            <h2>Reorder first</h2>
            <Link to="/reorder" className="link">
              Open planner
            </Link>
          </div>
          {reorder.error && <ErrorBanner message={reorder.error} onRetry={reorder.reload} />}
          {!reorder.data && !reorder.error && <LoadingBlock />}
          {reorder.data && reorder.data.length === 0 && (
            <EmptyState title="Nothing urgent">Every product has enough stock for its current sales rate.</EmptyState>
          )}
          {reorder.data && reorder.data.length > 0 && (
            <ul className="list">
              {reorder.data.map((item) => (
                <li key={item.productId}>
                  <div>
                    <strong>{item.name}</strong>
                    <span className="muted">
                      {item.stockOnHand} left
                      {item.daysOfCover !== null ? ` · ~${item.daysOfCover} days of cover` : ' · no recent sales'}
                    </span>
                  </div>
                  <div className="list-end">
                    <HealthBadge health={item.health} />
                    {item.suggestedOrderQuantity > 0 && <span className="muted">order {item.suggestedOrderQuantity}</span>}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <section className="panel">
        <div className="panel-header">
          <h2>Recent orders</h2>
          <Link to="/orders" className="link">
            All orders
          </Link>
        </div>
        {orders.error && <ErrorBanner message={orders.error} onRetry={orders.reload} />}
        {!orders.data && !orders.error && <LoadingBlock />}
        {orders.data && orders.data.items.length === 0 && <EmptyState title="No orders yet" />}
        {orders.data && orders.data.items.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Order</th>
                  <th>Customer</th>
                  <th>Placed</th>
                  <th className="num">Total</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {orders.data.items.map((order) => (
                  <tr key={order.id}>
                    <td data-label="Order">{order.orderNumber}</td>
                    <td data-label="Customer">{order.customerName}</td>
                    <td data-label="Placed">{formatDateTime(order.orderDate)}</td>
                    <td data-label="Total" className="num">{formatMoney(order.totalAmount)}</td>
                    <td data-label="Status">
                      <OrderStatusBadge status={order.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
