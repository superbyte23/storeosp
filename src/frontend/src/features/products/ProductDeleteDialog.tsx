import { useEffect, useRef, useState } from 'react'
import { request } from '../../lib/api'
import type { Product } from '../../shared/types'

type ProductDeleteDialogProps = {
  product: Product
  onCancel: () => void
  onDeleted: () => Promise<void>
}

export function ProductDeleteDialog({ product, onCancel, onDeleted }: ProductDeleteDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  async function confirmDelete() {
    setDeleting(true)
    setError('')
    try {
      await request<void>(`/api/v1/products/${product.id}`, { method: 'DELETE' })
      await onDeleted()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not delete product.')
      setDeleting(false)
    }
  }

  return (
    <dialog
      ref={dialogRef}
      className="product-dialog delete-dialog"
      aria-labelledby="delete-dialog-title"
      onCancel={event => {
        event.preventDefault()
        if (!deleting) onCancel()
      }}
      onClick={event => {
        if (event.target === event.currentTarget && !deleting) onCancel()
      }}
    >
      <div className="delete-dialog-mark" aria-hidden="true">!</div>
      <div className="product-dialog-heading delete-dialog-heading">
        <div>
          <h2 id="delete-dialog-title">Delete product?</h2>
          <p className="subtitle"><strong>{product.name}</strong> will be permanently removed from your catalog. This action cannot be undone.</p>
        </div>
        <button type="button" className="icon-button" onClick={onCancel} disabled={deleting} aria-label="Close dialog">×</button>
      </div>
      {error && <div className="alert modal-alert" role="alert">{error}</div>}
      <div className="modal-actions">
        <button type="button" className="button button-quiet" onClick={onCancel} disabled={deleting}>Cancel</button>
        <button type="button" className="button button-danger" onClick={() => void confirmDelete()} disabled={deleting}>
          {deleting ? 'Deleting…' : 'Delete product'}
        </button>
      </div>
    </dialog>
  )
}
