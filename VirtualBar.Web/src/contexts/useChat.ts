import { createContext, useContext } from 'react'
import type { ChatContextValue } from '../types'

// Lives apart from ChatProvider so ChatContext.tsx exports only a component and keeps Vite's fast refresh working.
export const ChatContext = createContext<ChatContextValue | undefined>(undefined)

export function useChat() {
  const context = useContext(ChatContext)
  if (context === undefined) {
    throw new Error('useChat must be used within ChatProvider')
  }
  return context
}
