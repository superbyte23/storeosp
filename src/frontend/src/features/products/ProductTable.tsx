import { money } from '../../shared/format'
import type { Product } from '../../shared/types'

type ProductTableProps = {
  products: Product[]
  onEdit?: (product: Product) => void
  onDelete?: (product: Product) => void
}

export function ProductTable({ products, onEdit, onDelete }: ProductTableProps) {
  return (
    <div className="table-scroll">
      <table>
        <thead>
          <tr><th>PRODUCT</th><th>SKU</th><th>CATEGORY</th><th>PRICE</th><th>IN STOCK</th><th>ACTIONS</th></tr>
        </thead>
        <tbody>
          {products.map(product => (
            <tr key={product.id}>
              <td>
                <div className="product-cell">
                  <div className="product-placeholder">{product.name.slice(0, 1)}</div>
                  <div><strong>{product.name}</strong><small>{product.description || 'No description'}</small></div>
                </div>
              </td>
              <td className="mono">{product.sku}</td>
              <td><span className="category-tag">{product.categoryName}</span></td>
              <td className="price-cell">{money.format(product.unitPrice)}</td>
              <td><span className={`stock-cell ${product.stockQuantity <= 5 ? 'stock-low' : ''}`}><i />{product.stockQuantity} units</span></td>
              <td>
                <div className="product-row-actions">
                  {onEdit && <button className="edit-action" aria-label={`Edit ${product.name}`} onClick={() => onEdit(product)}>Edit</button>}
                  {onDelete && <button className="delete-action" aria-label={`Delete ${product.name}`} onClick={() => onDelete(product)}>Delete</button>}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
