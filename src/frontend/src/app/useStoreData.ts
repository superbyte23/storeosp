import { useCallback, useEffect, useState } from 'react'
import { request } from '../lib/api'
import type { Dashboard, Category, Order, Page, Product } from '../shared/types'

export function useStoreData(authenticated: boolean, canViewOrders: boolean, canViewReports: boolean) {
  const [products, setProducts] = useState<Product[]>([])
  const [categories, setCategories] = useState<Category[]>([])
  const [orders, setOrders] = useState<Order[]>([])
  const [dashboard, setDashboard] = useState<Dashboard>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const refresh = useCallback(async () => {
    if (!authenticated) return
    setLoading(true)
    setError('')
    try {
      const [productPage, categoryList] = await Promise.all([
        request<Page<Product>>('/api/v1/products?pageSize=100'),
        request<Category[]>('/api/v1/categories'),
      ])
      setProducts(productPage.items)
      setCategories(categoryList)
      if (canViewOrders) {
        const orderPage = await request<Page<Order>>('/api/v1/orders?pageSize=100')
        setOrders(orderPage.items)
      } else {
        setOrders([])
      }
      if (canViewReports) {
        setDashboard(await request<Dashboard>('/api/v1/reports/dashboard'))
      } else {
        setDashboard(undefined)
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load StoreOps data.')
    } finally {
      setLoading(false)
    }
  }, [authenticated, canViewOrders, canViewReports])

  useEffect(() => {
    if (authenticated) void Promise.resolve().then(refresh)
  }, [authenticated, refresh])

  const reset = useCallback(() => {
    setProducts([])
    setCategories([])
    setOrders([])
    setDashboard(undefined)
    setLoading(false)
    setError('')
  }, [])

  const updateOrder = useCallback(async (order: Order, status: 'Fulfilled' | 'Cancelled') => {
    setError('')
    try {
      await request<Order>(`/api/v1/orders/${order.id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
      })
      await refresh()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not update order.')
    }
  }, [refresh])

  return {
    products,
    categories,
    orders,
    dashboard,
    loading,
    error,
    setError,
    reset,
    refresh,
    updateOrder,
  }
}
