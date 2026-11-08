import { useState } from 'react'
import type { FormEvent } from 'react'
import { login } from '../../lib/api'

export function LoginScreen({ error, onError }: { error: string; onError: (message: string) => void }) {
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true)
    onError('')
    try {
      await login(String(form.get('email')), String(form.get('password')))
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'Could not sign in.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-page">
      <form className="login-card" onSubmit={submit}>
        <a className="brand login-brand" href="#" aria-label="StoreOps">
          <span className="brand-mark">s</span><span>store<span className="brand-light">ops</span></span>
        </a>
        <p className="eyebrow">STORE WORKSPACE</p>
        <h1>Welcome back</h1>
        <p className="subtitle">Sign in to manage your store.</p>
        {error && <div className="alert" role="alert">{error}</div>}
        <label htmlFor="login-email">Email address</label>
        <input id="login-email" name="email" type="email" autoComplete="username" required />
        <label htmlFor="login-password">Password</label>
        <input id="login-password" name="password" type="password" autoComplete="current-password" required />
        <button className="button button-primary login-submit" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
