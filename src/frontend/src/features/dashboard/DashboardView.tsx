import { money } from '../../shared/format'
import type { Dashboard, Order, Product } from '../../shared/types'
import { OrderTable } from '../orders/OrderTable'

type DashboardViewProps = {
  dashboard?: Dashboard
  products: Product[]
  orders: Order[]
  canViewOrders: boolean
  canViewReports: boolean
  onOrderStatus: (order: Order, status: 'Fulfilled' | 'Cancelled') => void
  onNavigateToProducts: () => void
  onNavigateToOrders: () => void
}

export function DashboardView({
  dashboard,
  products,
  orders,
  canViewOrders,
  canViewReports,
  onOrderStatus,
  onNavigateToProducts,
  onNavigateToOrders,
}: DashboardViewProps) {
  return (
    <>
      {canViewReports && <StoreSummary dashboard={dashboard} />}
      <div className="overview-grid">
        <section className="panel inventory-panel">
          <div className="panel-heading"><div><h2>Inventory overview</h2><p>A quick look at what’s on your shelves.</p></div><button className="text-button" onClick={onNavigateToProducts}>View catalog <span>→</span></button></div>
          <div className="inventory-visual">
            <div className="ring-wrap"><div className="ring"><div><strong>{products.length}</strong><span>products</span></div></div></div>
            <div className="legend">
              <span><i className="legend-dot in-stock" />In stock <b>{products.filter(product => product.stockQuantity > 5).length}</b></span>
              <span><i className="legend-dot low-stock" />Running low <b>{products.filter(product => product.stockQuantity > 0 && product.stockQuantity <= 5).length}</b></span>
              <span><i className="legend-dot out-stock" />Out of stock <b>{products.filter(product => product.stockQuantity === 0).length}</b></span>
            </div>
          </div>
        </section>
        <section className="panel attention-panel">
          <div className="panel-heading"><div><h2>Needs attention</h2><p>Items running low on stock.</p></div><span className="attention-count">{dashboard?.lowStockCount ?? 0}</span></div>
          <div className="attention-list">
            {products.filter(product => product.stockQuantity <= 5).slice(0, 4).map(product => (
              <div className="attention-row" key={product.id}>
                <div className="product-placeholder">{product.name.slice(0, 1)}</div>
                <div className="attention-name"><strong>{product.name}</strong><small>{product.sku}</small></div>
                <span className={`stock-pill ${product.stockQuantity === 0 ? 'empty' : ''}`}>{product.stockQuantity} left</span>
              </div>
            ))}
            {products.every(product => product.stockQuantity > 5) && <p className="empty-note">Everything is well stocked. Nice work!</p>}
          </div>
        </section>
        {canViewOrders && (
          <section className="panel recent-panel">
            <div className="panel-heading"><div><h2>Recent orders</h2><p>Latest activity from your store.</p></div><button className="text-button" onClick={onNavigateToOrders}>All orders <span>→</span></button></div>
            <OrderTable orders={orders.slice(0, 4)} onStatus={onOrderStatus} />
          </section>
        )}
      </div>
    </>
  )
}

export function StoreSummary({ dashboard }: { dashboard?: Dashboard }) {
  return (
    <section className="stats-grid" aria-label="Store summary">
      <article className="stat-card"><div className="stat-top"><span>Total products</span><span className="stat-icon lilac">▦</span></div><strong>{dashboard?.productCount ?? 0}</strong><small><span className="positive">↗ 8.2%</span> vs last month</small><div className="sparkline purple" aria-hidden="true">▁▂▁▄▃▅▄▆▅▇▆█</div></article>
      <article className="stat-card"><div className="stat-top"><span>Low stock items</span><span className="stat-icon amber">⌁</span></div><strong>{dashboard?.lowStockCount ?? 0}</strong><small><span className="warning-text">Needs attention</span></small><div className="sparkline orange" aria-hidden="true">▆▅▆▄▅▃▄▂▃▂▁▂</div></article>
      <article className="stat-card"><div className="stat-top"><span>Open orders</span><span className="stat-icon blue">↗</span></div><strong>{dashboard?.pendingOrderCount ?? 0}</strong><small>Awaiting fulfillment</small><div className="sparkline blue-line" aria-hidden="true">▁▃▂▄▃▅▄▃▅▆▄█</div></article>
      <article className="stat-card"><div className="stat-top"><span>Revenue fulfilled</span><span className="stat-icon green">＄</span></div><strong>{money.format(dashboard?.fulfilledRevenue ?? 0)}</strong><small><span className="positive">↗ 12.4%</span> this month</small><div className="sparkline green-line" aria-hidden="true">▁▂▃▂▄▃▅▄▆▅▇█</div></article>
    </section>
  )
}
