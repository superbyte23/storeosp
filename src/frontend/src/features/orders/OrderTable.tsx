import { money } from '../../shared/format'
import type { Order } from '../../shared/types'

export function OrderTable({ orders, onStatus }: {
  orders: Order[]
  onStatus: (order: Order, status: 'Fulfilled' | 'Cancelled') => void
}) {
  if (orders.length === 0) return <div className="empty-note">No orders yet. Create one when you’re ready.</div>
  return (
    <div className="table-scroll">
      <table>
        <thead><tr><th>ORDER</th><th>DATE</th><th>ITEMS</th><th>TOTAL</th><th>STATUS</th><th /></tr></thead>
        <tbody>
          {orders.map(order => (
            <tr key={order.id}>
              <td className="mono order-id">#{order.id.slice(0, 8).toUpperCase()}</td>
              <td>{new Date(order.createdAtUtc).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</td>
              <td>{order.items.length} items</td>
              <td className="price-cell">{money.format(order.total)}</td>
              <td><span className={`status-pill status-${order.status.toLowerCase()}`}><i />{order.status}</span></td>
              <td>{order.status === 'Pending' && <div className="order-actions"><button title="Mark fulfilled" aria-label="Mark order fulfilled" onClick={() => onStatus(order, 'Fulfilled')}>✓</button><button title="Cancel order" aria-label="Cancel order" onClick={() => onStatus(order, 'Cancelled')}>×</button></div>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
