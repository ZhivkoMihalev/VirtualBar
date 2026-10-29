import { useState, useCallback } from 'react'
import type { ChatContextValue } from '../types'
import { ChatContext } from './useChat'

export function ChatProvider({ children }: { children: React.ReactNode }) {
  const [inboxOpen, setInboxOpen] = useState(false)
  const [activeUserId, setActiveUserId] = useState<string | null>(null)

  const openInbox = useCallback(() => setInboxOpen(true), [])
  const closeInbox = useCallback(() => setInboxOpen(false), [])
  const toggleInbox = useCallback(() => setInboxOpen(o => !o), [])

  const openChat = useCallback((userId: string) => {
    setActiveUserId(userId)
    setInboxOpen(true)
  }, [])

  const closeChat = useCallback(() => setActiveUserId(null), [])

  const value: ChatContextValue = {
    inboxOpen,
    openInbox,
    closeInbox,
    toggleInbox,
    activeUserId,
    openChat,
    closeChat,
  }

  return <ChatContext.Provider value={value}>{children}</ChatContext.Provider>
}
