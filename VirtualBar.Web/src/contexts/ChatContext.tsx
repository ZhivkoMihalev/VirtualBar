import { createContext, useContext, useState, useCallback } from 'react'

interface ChatContextValue {
  inboxOpen: boolean
  openInbox: () => void
  closeInbox: () => void
  toggleInbox: () => void
  activeUserId: string | null
  openChat: (userId: string) => void
  closeChat: () => void
}

const ChatContext = createContext<ChatContextValue | undefined>(undefined)

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

export function useChat() {
  const context = useContext(ChatContext)
  if (context === undefined) {
    throw new Error('useChat must be used within ChatProvider')
  }
  return context
}
