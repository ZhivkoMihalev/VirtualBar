import { createContext, useContext } from 'react'
import type { AuthContextValue } from '../types'

// Lives apart from AuthProvider so AuthContext.tsx exports only a component and keeps Vite's fast refresh working.
export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return context
}
