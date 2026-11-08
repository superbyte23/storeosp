import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { request } from '../../lib/api'
import type { Category, Product } from '../../shared/types'

type ProductModalProps = {
  categories: Category[]
  product?: Product
  onClose: () => void
  onSaved: () => void
}

export function ProductModal({ categories, product, onClose, onSaved }: ProductModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const nameInputRef = useRef<HTMLInputElement>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  useEffect(() => {
    dialogRef.current?.showModal()
    nameInputRef.current?.focus()
  }, [])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    const saveAndNew = submitter instanceof HTMLButtonElement && submitter.value === 'new'
    setSaving(true)
    setError('')
    setMessage('')

    try {
      await request<Product>(product ? `/api/v1/products/${product.id}` : '/api/v1/products', {
        method: product ? 'PUT' : 'POST',
        body: JSON.stringify({
          categoryId: form.get('categoryId'),
          sku: form.get('sku'),
          name: form.get('name'),
          description: form.get('description'),
          unitPrice: Number(form.get('unitPrice')),
          stockQuantity: Number(form.get('stockQuantity')),
        }),
      })
      onSaved()
      if (saveAndNew && !product) {
        formElement.reset()
        setMessage('Product saved. Enter the next product.')
        nameInputRef.current?.focus()
      } else {
        onClose()
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not save product.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <dialog
      ref={dialogRef}
      className="product-dialog"
      aria-labelledby="product-dialog-title"
      onCancel={event => {
        event.preventDefault()
        if (!saving) onClose()
      }}
      onClick={event => {
        if (event.target === event.currentTarget && !saving) onClose()
      }}
    >
      <div className="product-dialog-heading">
        <div>
          <p className="eyebrow">PRODUCT CATALOG</p>
          <h2 id="product-dialog-title">{product ? 'Edit product' : 'Add a product'}</h2>
          <p className="subtitle">{product ? 'Update the product details and inventory quantity.' : 'Enter the product details and inventory quantity.'}</p>
        </div>
        <button type="button" className="icon-button" onClick={onClose} disabled={saving} aria-label="Close dialog">×</button>
      </div>
      <form className="product-modal-form" onSubmit={submit}>
        {error && <div className="alert modal-alert" role="alert">{error}</div>}
        {message && <p className="modal-success" role="status">{message}</p>}
        <label>Product name<input ref={nameInputRef} name="name" required maxLength={200} placeholder="e.g. House blend coffee" defaultValue={product?.name ?? ''} autoFocus /></label>
        <label>SKU<input name="sku" required maxLength={64} placeholder="e.g. BEV-003" defaultValue={product?.sku ?? ''} /></label>
        <label>Category<select name="categoryId" required defaultValue={product?.categoryId ?? ''}><option value="" disabled>Select category</option>{categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
        <label>Price (USD)<input name="unitPrice" type="number" required min="0" step="0.01" placeholder="0.00" defaultValue={product?.unitPrice ?? ''} /></label>
        <label>Quantity<input name="stockQuantity" type="number" required min="0" step="1" placeholder="0" defaultValue={product?.stockQuantity ?? ''} /></label>
        <label className="modal-wide-field">Description <span className="optional-label">Optional</span><textarea name="description" maxLength={2000} rows={3} placeholder="Add a short product description" defaultValue={product?.description ?? ''} /></label>
        <div className="modal-actions">
          <button type="button" className="button button-quiet" onClick={onClose} disabled={saving}>Cancel</button>
          {!product && <button type="submit" className="button button-quiet" name="intent" value="new" disabled={saving}>{saving ? 'Saving…' : 'Save and New'}</button>}
          <button type="submit" className="button button-primary" disabled={saving}>{saving ? 'Saving…' : product ? 'Save changes' : 'Save product'}</button>
        </div>
      </form>
    </dialog>
  )
}
