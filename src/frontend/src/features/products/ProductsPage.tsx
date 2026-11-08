import { useMemo, useState } from 'react'
import type { Category, Product } from '../../shared/types'
import { ProductDeleteDialog } from './ProductDeleteDialog'
import { ProductModal } from './ProductModal'
import { ProductTable } from './ProductTable'

type ProductsPageProps = {
  products: Product[]
  categories: Category[]
  canManage: boolean
  onChanged: () => void
}

export function ProductsPage({ products, categories, canManage, onChanged }: ProductsPageProps) {
  const [search, setSearch] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [editingProduct, setEditingProduct] = useState<Product | null>(null)
  const [deletingProduct, setDeletingProduct] = useState<Product | null>(null)
  const [error, setError] = useState('')

  const visibleProducts = useMemo(() => {
    const term = search.trim().toLowerCase()
    return term ? products.filter(product =>
      `${product.name} ${product.sku} ${product.categoryName}`.toLowerCase().includes(term),
    ) : products
  }, [products, search])

  return (
    <>
      {error && <div className="alert" role="alert"><span>{error}</span><button onClick={() => setError('')} aria-label="Dismiss error">×</button></div>}
      <section className="panel table-panel">
        <div className="table-toolbar">
          <div>
            <h2>Product catalog <span className="subtle-count">{products.length}</span></h2>
            <p>Manage your products, pricing, and stock levels.</p>
            {canManage && <button className="button button-primary products-add-button" onClick={() => { setEditingProduct(null); setShowForm(true) }}><span>＋</span> Add product</button>}
          </div>
          <label className="search-field"><span>⌕</span><input aria-label="Search products" placeholder="Search products…" value={search} onChange={event => setSearch(event.target.value)} /><kbd>⌘ K</kbd></label>
        </div>
        <ProductTable
          products={visibleProducts}
          onEdit={canManage ? product => { setEditingProduct(product); setShowForm(true) } : undefined}
          onDelete={canManage ? setDeletingProduct : undefined}
        />
        {visibleProducts.length === 0 && <div className="empty-note">No products match your search.</div>}
      </section>
      {showForm && canManage && (
        <ProductModal
          key={editingProduct?.id ?? 'new'}
          categories={categories}
          product={editingProduct ?? undefined}
          onClose={() => setShowForm(false)}
          onSaved={onChanged}
        />
      )}
      {deletingProduct && (
        <ProductDeleteDialog
          product={deletingProduct}
          onCancel={() => setDeletingProduct(null)}
          onDeleted={async () => {
            await onChanged()
            setDeletingProduct(null)
          }}
        />
      )}
    </>
  )
}
