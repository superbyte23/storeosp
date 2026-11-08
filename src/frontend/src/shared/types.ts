export type Category = { id: string; name: string }

export type Product = {
  id: string
  categoryId: string
  categoryName: string
  sku: string
  name: string
  description: string
  unitPrice: number
  stockQuantity: number
  createdAtUtc: string
}

export type Order = {
  id: string
  status: 'Pending' | 'Fulfilled' | 'Cancelled'
  createdAtUtc: string
  total: number
  items: { productId: string; productName: string; quantity: number; unitPrice: number }[]
}

export type Dashboard = {
  productCount: number
  lowStockCount: number
  pendingOrderCount: number
  fulfilledRevenue: number
}

export type Page<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export type Section = 'Overview' | 'Products' | 'Orders'
