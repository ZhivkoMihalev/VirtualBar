import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import * as authApi from '@/api/authApi'
import { TOKEN_KEY, USER_KEY } from '@/api/client'
import { AuthProvider } from '@/contexts/AuthContext'
import { useAuth } from '@/contexts/useAuth'
import type { RegisterOutcome, User } from '@/types'

vi.mock('@/api/authApi')

const user: User = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'collector@example.com',
  displayName: 'Collector',
  createdAt: '2026-10-31T10:00:00Z',
  isAdmin: false,
}

function renderAuth() {
  return renderHook(() => useAuth(), { wrapper: AuthProvider })
}

async function register(result: ReturnType<typeof renderAuth>['result']): Promise<RegisterOutcome> {
  let outcome: RegisterOutcome | undefined
  await act(async () => {
    outcome = await result.current.register(user.email, 'Passw0rd!', user.displayName, 'en')
  })
  return outcome!
}

describe('AuthContext.register', () => {
  it('stores nothing and stays logged out when the API asks for email confirmation', async () => {
    vi.mocked(authApi.register).mockResolvedValue({ token: '', user: null, requiresEmailConfirmation: true })
    const { result } = renderAuth()

    const outcome = await register(result)

    expect(outcome).toBe('emailConfirmationRequired')
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull()
    expect(localStorage.getItem(USER_KEY)).toBeNull()
    expect(result.current.isAuthenticated).toBe(false)
    expect(result.current.user).toBeNull()
  })

  it('stores the session and signs in when the API issues a token', async () => {
    vi.mocked(authApi.register).mockResolvedValue({ token: 'jwt-token', user, requiresEmailConfirmation: false })
    const { result } = renderAuth()

    const outcome = await register(result)

    expect(outcome).toBe('signedIn')
    expect(localStorage.getItem(TOKEN_KEY)).toBe('jwt-token')
    expect(JSON.parse(localStorage.getItem(USER_KEY)!)).toEqual(user)
    expect(result.current.isAuthenticated).toBe(true)
    expect(result.current.user).toEqual(user)
  })

  it('sends the email language to the API', async () => {
    vi.mocked(authApi.register).mockResolvedValue({ token: '', user: null, requiresEmailConfirmation: true })
    const { result } = renderAuth()

    await register(result)

    expect(authApi.register).toHaveBeenCalledWith(user.email, 'Passw0rd!', user.displayName, 'en')
  })
})

describe('AuthContext saved session', () => {
  it('is signed in on the first render when a session is saved', () => {
    localStorage.setItem(TOKEN_KEY, 'jwt-token')
    localStorage.setItem(USER_KEY, JSON.stringify(user))

    const { result } = renderAuth()

    expect(result.current.isAuthenticated).toBe(true)
    expect(result.current.user).toEqual(user)
  })

  it('drops a saved session whose user cannot be read', () => {
    localStorage.setItem(TOKEN_KEY, 'jwt-token')
    localStorage.setItem(USER_KEY, '{not json')

    const { result } = renderAuth()

    expect(result.current.isAuthenticated).toBe(false)
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull()
    expect(localStorage.getItem(USER_KEY)).toBeNull()
  })
})
