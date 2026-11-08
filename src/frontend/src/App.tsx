import { useEffect, useState } from 'react'
import { getSessionRoles, hasSession, logout } from './lib/api'
import { StoreSidebar } from './app/StoreSidebar'
import { useStoreData } from './app/useStoreData'
import { LoginScreen } from './features/auth/LoginScreen'
import { DashboardView, StoreSummary } from './features/dashboard/DashboardView'
import { OrdersPage } from './features/orders/OrdersPage'
import { ProductsPage } from './features/products/ProductsPage'
import type { Section } from './shared/types'
import './App.css'

function App() {
  const [section, setSection] = useState<Section>('Overview')
  const [authenticated, setAuthenticated] = useState(hasSession)
  const [roles, setRoles] = useState(getSessionRoles)
  const [error, setError] = useState('')

  const canViewOrders = roles.some(role => ['Admin', 'Manager', 'Fulfillment'].includes(role))
  const canViewReports = roles.some(role => ['Admin', 'Manager', 'Viewer'].includes(role))
  const canManageProducts = roles.some(role => ['Admin', 'Manager'].includes(role))
  const store = useStoreData(authenticated, canViewOrders, canViewReports)
  const resetStoreData = store.reset

  useEffect(() => {
    const onAuthenticated = () => {
      setRoles(getSessionRoles())
      setAuthenticated(true)
      setError('')
    }
    const onUnauthenticated = (event: Event) => {
      setAuthenticated(false)
      setRoles([])
      setSection('Overview')
      resetStoreData()
      setError(event instanceof CustomEvent && typeof event.detail === 'string' ? event.detail : '')
    }
    window.addEventListener('storeops:authenticated', onAuthenticated)
    window.addEventListener('storeops:unauthenticated', onUnauthenticated)
    return () => {
      window.removeEventListener('storeops:authenticated', onAuthenticated)
      window.removeEventListener('storeops:unauthenticated', onUnauthenticated)
    }
  }, [resetStoreData])

  function signOut() {
    void logout().catch(cause => {
      setError(cause instanceof Error ? cause.message : 'Could not revoke the session.')
    })
  }

  if (!authenticated)
    return <LoginScreen onError={setError} error={error} />

  return (
    <div className="app-shell">
      <StoreSidebar
        section={section}
        roles={roles}
        dashboard={store.dashboard}
        canViewOrders={canViewOrders}
        onNavigate={setSection}
        onSignOut={signOut}
      />
      <main className="main-content">
        <header className="topbar">
          <div className="breadcrumb"><span>Workspace</span><span className="slash">/</span><strong>{section}</strong></div>
          <div className="top-actions"><span className="status-dot" /> All systems operational <button className="top-avatar" onClick={signOut} aria-label="Sign out">↪</button></div>
        </header>
        <div className="page-content">
          {(error || store.error) && (
            <div className="alert" role="alert">
              <span>{error || store.error}</span>
              <button onClick={() => { setError(''); store.setError('') }} aria-label="Dismiss error">×</button>
            </div>
          )}
          <div className="page-heading">
            <div>
              <p className="eyebrow">{section === 'Overview' ? 'STORE WORKSPACE' : 'STORE MANAGEMENT'}</p>
              <h1>{section}</h1>
              <p className="subtitle">
                {section === 'Overview'
                  ? 'Here’s what’s happening with your store today.'
                  : section === 'Products'
                    ? 'Keep your catalog accurate and your shelves in sync.'
                    : 'Track orders from checkout to fulfillment.'}
              </p>
            </div>
            <div className="heading-actions">
              <button className="button button-quiet" onClick={() => void store.refresh()}>↻ <span>Refresh</span></button>
              {section === 'Products' && canManageProducts && (
                <button className="button button-primary" onClick={() => setSection('Products')}>
                  <span>＋</span> Add product
                </button>
              )}
            </div>
          </div>
          {store.loading ? (
            <div className="loading-state"><span className="spinner" /> Loading your store…</div>
          ) : (
            <>
              {section === 'Overview' && (
                <DashboardView
                  dashboard={store.dashboard}
                  products={store.products}
                  orders={store.orders}
                  canViewOrders={canViewOrders}
                  canViewReports={canViewReports}
                  onOrderStatus={store.updateOrder}
                  onNavigateToProducts={() => setSection('Products')}
                  onNavigateToOrders={() => setSection('Orders')}
                />
              )}
              {section === 'Products' && (
                <>
                  {canViewReports && (
                    <StoreSummary dashboard={store.dashboard} />
                  )}
                  <ProductsPage
                    products={store.products}
                    categories={store.categories}
                    canManage={canManageProducts}
                    onChanged={store.refresh}
                  />
                </>
              )}
              {section === 'Orders' && canViewOrders && (
                <OrdersPage orders={store.orders} onStatus={store.updateOrder} />
              )}
            </>
          )}
          <footer className="footer"><span>© 2026 StoreOps</span><span>Made for the people behind the shelves.</span></footer>
        </div>
      </main>
    </div>
  )
}

export default App
