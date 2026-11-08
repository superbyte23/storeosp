import type { Order } from '../../shared/types'
import { OrderTable } from './OrderTable'

export function OrdersPage({ orders, onStatus }: {
  orders: Order[]
  onStatus: (order: Order, status: 'Fulfilled' | 'Cancelled') => void
}) {
  return (
    <section className="panel table-panel">
      <div className="table-toolbar"><div><h2>Order activity <span className="subtle-count">{orders.length}</span></h2><p>Review and fulfill customer orders.</p></div></div>
      <OrderTable orders={orders} onStatus={onStatus} />
    </section>
  )
}
