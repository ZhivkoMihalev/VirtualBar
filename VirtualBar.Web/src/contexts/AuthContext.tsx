import { useState } from 'react'
import type { AuthContextValue, User } from '../types'
import * as authApi from '../api/authApi'
import { TOKEN_KEY, USER_KEY } from '../api/client'
import { AuthContext } from './useAuth'

// localStorage is synchronous, so the saved session is read while the state is created: the first render
// already knows who is logged in, instead of rendering logged-out and correcting itself in an effect.
function readStoredSession(): { token: string; user: User } | null {
  const storedToken = localStorage.getItem(TOKEN_KEY)
  const storedUser = localStorage.getItem(USER_KEY)

  if (!storedToken || !storedUser) return null

  try {
    return { token: storedToken, user: JSON.parse(storedUser) }
  } catch {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    return null
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [storedSession] = useState(readStoredSession)
  const [user, setUser] = useState<User | null>(storedSession?.user ?? null)
  const [token, setToken] = useState<string | null>(storedSession?.token ?? null)

  // login and register deliberately set no app-wide loading state: the routes would swap to a spinner,
  // unmount the form, and lose the error a failed attempt sets on it. The forms show their own pending state.
  const login = async (email: string, password: string) => {
    const { token: newToken, user: newUser } = await authApi.login(email, password)

    localStorage.setItem(TOKEN_KEY, newToken)
    localStorage.setItem(USER_KEY, JSON.stringify(newUser))

    setToken(newToken)
    setUser(newUser)
  }

  const register = async (email: string, password: string, displayName: string) => {
    const { token: newToken, user: newUser } = await authApi.register(email, password, displayName)

    localStorage.setItem(TOKEN_KEY, newToken)
    localStorage.setItem(USER_KEY, JSON.stringify(newUser))

    setToken(newToken)
    setUser(newUser)
  }

  const logout = () => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    setToken(null)
    setUser(null)
    authApi.logout().catch(() => {
      // Ignore logout errors
    })
  }

  const updateUser = (patch: Partial<User>) => {
    setUser(prev => {
      if (!prev) return prev
      const updated = { ...prev, ...patch }
      localStorage.setItem(USER_KEY, JSON.stringify(updated))
      return updated
    })
  }

  const value: AuthContextValue = {
    user,
    token,
    isAuthenticated: !!token && !!user,
    login,
    register,
    logout,
    updateUser,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
