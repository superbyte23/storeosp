export type TokenResponse = {
  accessToken: string
  refreshToken: string
  expiresIn: number
}

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

const ACCESS_TOKEN_KEY = 'storeops.access-token'
const REFRESH_TOKEN_KEY = 'storeops.refresh-token'
let refreshInFlight: Promise<TokenResponse> | null = null

function readSession(): TokenResponse | null {
  const accessToken = sessionStorage.getItem(ACCESS_TOKEN_KEY)
  const refreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY)
  return accessToken && refreshToken
    ? { accessToken, refreshToken, expiresIn: 0 }
    : null
}

export function saveSession(tokens: TokenResponse) {
  sessionStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken)
  sessionStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken)
  window.dispatchEvent(new Event('storeops:authenticated'))
}

export function clearSession(message?: string) {
  sessionStorage.removeItem(ACCESS_TOKEN_KEY)
  sessionStorage.removeItem(REFRESH_TOKEN_KEY)
  window.dispatchEvent(new CustomEvent('storeops:unauthenticated', { detail: message }))
}

export function hasSession() {
  return readSession() !== null
}

export function getSessionRoles(): string[] {
  const session = readSession()
  if (!session) return []

  try {
    const payload = session.accessToken.split('.')[1]
    if (!payload) return []
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const claims = JSON.parse(atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '='))) as Record<string, unknown>
    const roleClaim = claims.role ?? claims.roles ??
      claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
    if (typeof roleClaim === 'string') return [roleClaim]
    if (Array.isArray(roleClaim)) return roleClaim.filter((role): role is string => typeof role === 'string')
  } catch {
    return []
  }

  return []
}

async function rotateRefreshToken(): Promise<TokenResponse> {
  const current = readSession()
  if (!current) throw new ApiError('Your session has expired. Please sign in again.', 401)

  const response = await fetch('/api/v1/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: current.refreshToken }),
  })

  if (response.status === 401) {
    clearSession('Your session has expired. Please sign in again.')
    throw new ApiError('Your session has expired. Please sign in again.', 401)
  }
  if (!response.ok)
    throw new ApiError(await readError(response), response.status)

  const tokens = await response.json() as TokenResponse
  saveSession(tokens)
  return tokens
}

async function getFreshTokens() {
  if (!refreshInFlight) {
    refreshInFlight = rotateRefreshToken().finally(() => {
      refreshInFlight = null
    })
  }
  return refreshInFlight
}

async function send(path: string, init: RequestInit, token?: string): Promise<Response> {
  const headers = new Headers(init.headers)
  if (init.body && !headers.has('Content-Type'))
    headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  return fetch(path, { ...init, headers })
}

export async function login(email: string, password: string): Promise<TokenResponse> {
  const response = await send('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
  if (!response.ok) {
    const error = await readError(response)
    throw new ApiError(error, response.status)
  }
  const tokens = await response.json() as TokenResponse
  saveSession(tokens)
  return tokens
}

export async function logout() {
  const current = readSession()
  try {
    if (current) {
      await request<void>('/api/v1/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      })
      const latest = readSession()
      if (latest && latest.refreshToken !== current.refreshToken) {
        await request<void>('/api/v1/auth/logout', {
          method: 'POST',
          body: JSON.stringify({ refreshToken: latest.refreshToken }),
        })
      }
    }
  } finally {
    clearSession()
  }
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let session = readSession()
  let response = await send(path, init, session?.accessToken)

  if (response.status === 401 && session && !path.startsWith('/api/v1/auth/')) {
    session = await getFreshTokens()
    response = await send(path, init, session.accessToken)
    if (response.status === 401) {
      clearSession('Your renewed session was rejected. Please sign in again.')
      throw new ApiError('Your renewed session was rejected. Please sign in again.', 401)
    }
  }

  if (!response.ok)
    throw new ApiError(await readError(response), response.status)

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

async function readError(response: Response) {
  const body = await response.json().catch(() => null) as {
    title?: string
    detail?: string
    errors?: Record<string, string[]>
  } | null
  const validation = body?.errors ? Object.values(body.errors).flat().join(' ') : ''
  return validation || body?.detail || body?.title || `Request failed (${response.status})`
}
