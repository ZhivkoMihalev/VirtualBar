import { useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import { useChat } from '../contexts/ChatContext'
import { getInbox, getConversation, sendMessage, markRead } from '../api/messagesApi'
import type { ConversationSummary, Message } from '../types'
import Avatar from './Avatar'

const containerStyle: CSSProperties = {
  position: 'fixed',
  bottom: 0,
  right: 20,
  zIndex: 1000,
  display: 'flex',
  alignItems: 'flex-end',
  gap: 12,
  pointerEvents: 'none',
}

const toggleButtonStyle: CSSProperties = {
  pointerEvents: 'auto',
  position: 'relative',
  width: 52,
  height: 52,
  borderRadius: '50%',
  border: 'none',
  cursor: 'pointer',
  marginBottom: 20,
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  color: '#07030A',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  boxShadow: '0 6px 24px rgba(0,0,0,0.5)',
}

const toggleBadgeStyle: CSSProperties = {
  position: 'absolute',
  top: -4,
  right: -4,
  minWidth: 20,
  height: 20,
  padding: '0 5px',
  borderRadius: 10,
  background: '#D42020',
  color: '#FFF',
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  fontWeight: 700,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  lineHeight: 1,
}

const panelStyle: CSSProperties = {
  pointerEvents: 'auto',
  width: 300,
  height: 480,
  display: 'flex',
  flexDirection: 'column',
  background: 'rgba(7,3,10,0.97)',
  border: '1px solid rgba(201,168,76,0.25)',
  borderBottom: 'none',
  borderRadius: '8px 8px 0 0',
  boxShadow: '0 8px 32px rgba(0,0,0,0.6)',
  overflow: 'hidden',
}

const chatWindowStyle: CSSProperties = {
  pointerEvents: 'auto',
  width: 320,
  height: 480,
  display: 'flex',
  flexDirection: 'column',
  background: 'rgba(7,3,10,0.97)',
  border: '1px solid rgba(201,168,76,0.25)',
  borderBottom: 'none',
  borderRadius: '8px 8px 0 0',
  boxShadow: '0 8px 32px rgba(0,0,0,0.6)',
  overflow: 'hidden',
}

const panelHeaderStyle: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  padding: '14px 16px',
  borderBottom: '1px solid rgba(201,168,76,0.12)',
  flexShrink: 0,
}

const panelTitleStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 13,
  letterSpacing: '0.25em',
  color: '#E8C870',
}

const chatNameStyle: CSSProperties = {
  fontFamily: 'Playfair Display, serif',
  fontSize: 18,
  color: '#E8C870',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
}

const closeButtonStyle: CSSProperties = {
  background: 'transparent',
  border: 'none',
  cursor: 'pointer',
  color: '#C9A84C',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 24,
  lineHeight: 1,
  padding: 0,
  flexShrink: 0,
}

const listStyle: CSSProperties = {
  flex: 1,
  overflowY: 'auto',
}

const stateStyle: CSSProperties = {
  padding: '40px 16px',
  textAlign: 'center',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 16,
  fontStyle: 'italic',
  color: '#B09868',
}

const loadingStyle: CSSProperties = {
  padding: '40px 16px',
  textAlign: 'center',
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.3em',
  color: '#C9A84C',
}

const errorStyle: CSSProperties = {
  padding: '40px 16px',
  textAlign: 'center',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 16,
  fontStyle: 'italic',
  color: '#C04040',
}

const messageAreaStyle: CSSProperties = {
  flex: 1,
  overflowY: 'auto',
  padding: 16,
  display: 'flex',
  flexDirection: 'column',
  gap: 10,
}

const inputAreaStyle: CSSProperties = {
  display: 'flex',
  gap: 8,
  padding: '12px 14px',
  borderTop: '1px solid rgba(201,168,76,0.12)',
  flexShrink: 0,
}

const textareaStyle: CSSProperties = {
  flex: 1,
  resize: 'none',
  background: 'rgba(255,255,255,0.04)',
  border: '1px solid rgba(201,168,76,0.2)',
  borderRadius: 4,
  padding: '8px 12px',
  color: '#F0DDB4',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 15,
  outline: 'none',
}

const sendButtonStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  padding: '0 16px',
  borderRadius: 2,
  color: '#07030A',
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  border: 'none',
  flexShrink: 0,
}

function formatTime(iso: string): string {
  const date = new Date(iso)
  const now = new Date()
  const sameDay =
    date.getFullYear() === now.getFullYear() &&
    date.getMonth() === now.getMonth() &&
    date.getDate() === now.getDate()

  if (sameDay) {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  }

  return date.toLocaleDateString([], { day: '2-digit', month: 'short' })
}

function ConversationCard({
  conversation,
  selected,
  you,
  onSelect,
}: {
  conversation: ConversationSummary
  selected: boolean
  you: string
  onSelect: (userId: string) => void
}) {
  const preview =
    conversation.lastMessageContent.length > 40
      ? `${conversation.lastMessageContent.slice(0, 40)}…`
      : conversation.lastMessageContent

  return (
    <button
      onClick={() => onSelect(conversation.otherUserId)}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        width: '100%',
        textAlign: 'left',
        padding: '12px 14px',
        background: selected ? 'rgba(201,168,76,0.08)' : 'transparent',
        borderLeft: selected ? '3px solid #C9A84C' : '3px solid transparent',
        borderTop: 'none',
        borderRight: 'none',
        borderBottom: '1px solid rgba(201,168,76,0.08)',
        cursor: 'pointer',
      }}
    >
      <Avatar displayName={conversation.otherUserDisplayName} avatarUrl={conversation.otherUserAvatarUrl} size={40} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 8 }}>
          <span
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontSize: 16,
              color: '#E8C870',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {conversation.otherUserDisplayName}
          </span>
          <span style={{ fontFamily: 'Cinzel, serif', fontSize: 9, color: '#806840', flexShrink: 0 }}>
            {formatTime(conversation.lastMessageAt)}
          </span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, marginTop: 2 }}>
          <span
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontSize: 13,
              color: '#B09868',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {conversation.lastMessageIsFromMe ? `${you}: ${preview}` : preview}
          </span>
          {conversation.unreadCount > 0 && (
            <span
              style={{
                flexShrink: 0,
                minWidth: 18,
                height: 18,
                borderRadius: '50%',
                background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                color: '#07030A',
                fontFamily: 'Cinzel, serif',
                fontSize: 10,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: '0 5px',
              }}
            >
              {conversation.unreadCount}
            </span>
          )}
        </div>
      </div>
    </button>
  )
}

function MessageBubble({ message, mine }: { message: Message; mine: boolean }) {
  return (
    <div style={{ display: 'flex', justifyContent: mine ? 'flex-end' : 'flex-start' }}>
      <div
        style={{
          maxWidth: '75%',
          padding: '8px 12px',
          borderRadius: 8,
          background: mine ? 'rgba(201,168,76,0.15)' : 'rgba(255,255,255,0.05)',
          border: mine ? '1px solid rgba(201,168,76,0.3)' : '1px solid rgba(255,255,255,0.08)',
        }}
      >
        <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#F0DDB4', lineHeight: 1.4, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
          {message.content}
        </div>
        <div style={{ fontFamily: 'Cinzel, serif', fontSize: 8, letterSpacing: '0.1em', color: '#806840', marginTop: 4, textAlign: 'right' }}>
          {formatTime(message.createdAt)}
        </div>
      </div>
    </div>
  )
}

function InboxPanel({
  onSelect,
  onClose,
  selectedUserId,
}: {
  onSelect: (userId: string) => void
  onClose: () => void
  selectedUserId: string | null
}) {
  const { t } = useTranslation()

  const { data: inbox = [], isLoading, isError } = useQuery({
    queryKey: ['inbox'],
    queryFn: getInbox,
    staleTime: 30_000,
    refetchInterval: 30_000,
  })

  return (
    <div style={panelStyle}>
      <div style={panelHeaderStyle}>
        <span style={panelTitleStyle}>{t('messages.title')}</span>
        <button onClick={onClose} style={closeButtonStyle} aria-label={t('messages.title')}>
          ×
        </button>
      </div>

      <div style={listStyle}>
        {isLoading && <div style={loadingStyle}>{t('messages.loading')}</div>}

        {isError && !isLoading && <div style={errorStyle}>{t('messages.error')}</div>}

        {!isLoading && !isError && inbox.length === 0 && (
          <div style={stateStyle}>{t('messages.noConversations')}</div>
        )}

        {!isLoading && !isError &&
          inbox.map(conversation => (
            <ConversationCard
              key={conversation.otherUserId}
              conversation={conversation}
              selected={conversation.otherUserId === selectedUserId}
              you={t('messages.you')}
              onSelect={onSelect}
            />
          ))}
      </div>
    </div>
  )
}

function ChatWindow({ userId, onClose }: { userId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState('')
  const scrollRef = useRef<HTMLDivElement>(null)

  const inbox = queryClient.getQueryData<ConversationSummary[]>(['inbox'])

  const {
    data: thread = [],
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['conversation', userId],
    queryFn: () => getConversation(userId),
    enabled: !!userId,
  })

  const markReadMutation = useMutation({
    mutationFn: markRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
    },
  })

  const sendMutation = useMutation({
    mutationFn: (content: string) => sendMessage(userId, content),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
      queryClient.invalidateQueries({ queryKey: ['conversation', userId] })
    },
  })

  const currentUserId = user?.id ?? ''

  const unreadIds = useMemo(
    () =>
      thread
        .filter(m => !m.isRead && m.senderId !== currentUserId)
        .map(m => m.id),
    [thread, currentUserId],
  )

  const markReadMutate = markReadMutation.mutate
  useEffect(() => {
    for (const id of unreadIds) {
      markReadMutate(id)
    }
  }, [unreadIds, markReadMutate])

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
    }
  }, [thread])

  const headerName =
    inbox?.find(c => c.otherUserId === userId)?.otherUserDisplayName ??
    thread.find(m => m.senderId === userId)?.senderDisplayName ??
    ''

  const handleSend = () => {
    const content = draft.trim()
    if (!content || sendMutation.isPending) {
      return
    }
    sendMutation.mutate(content)
    setDraft('')
  }

  return (
    <div style={chatWindowStyle}>
      <div style={panelHeaderStyle}>
        <span style={chatNameStyle}>{headerName}</span>
        <button onClick={onClose} style={closeButtonStyle} aria-label={t('messages.title')}>
          ×
        </button>
      </div>

      <div ref={scrollRef} style={messageAreaStyle}>
        {isLoading && <div style={loadingStyle}>{t('messages.loading')}</div>}

        {isError && !isLoading && <div style={errorStyle}>{t('messages.error')}</div>}

        {!isLoading && !isError &&
          thread.map(message => (
            <MessageBubble key={message.id} message={message} mine={message.senderId === currentUserId} />
          ))}
      </div>

      <div style={inputAreaStyle}>
        <textarea
          value={draft}
          onChange={e => setDraft(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault()
              handleSend()
            }
          }}
          placeholder={t('messages.inputPlaceholder')}
          rows={1}
          style={textareaStyle}
        />
        <button
          onClick={handleSend}
          disabled={!draft.trim() || sendMutation.isPending}
          style={{
            ...sendButtonStyle,
            cursor: !draft.trim() || sendMutation.isPending ? 'not-allowed' : 'pointer',
            opacity: !draft.trim() || sendMutation.isPending ? 0.5 : 1,
          }}
        >
          {sendMutation.isPending ? t('messages.sending') : t('messages.send')}
        </button>
      </div>
    </div>
  )
}

export default function ChatWidget() {
  const { t } = useTranslation()
  const { isAuthenticated } = useAuth()
  const { inboxOpen, toggleInbox, closeInbox, activeUserId, openChat, closeChat } = useChat()

  const { data: inbox = [] } = useQuery({
    queryKey: ['inbox'],
    queryFn: getInbox,
    staleTime: 30_000,
    refetchInterval: 30_000,
    enabled: isAuthenticated,
  })

  if (!isAuthenticated) {
    return null
  }

  const totalUnread = inbox.reduce((sum, c) => sum + c.unreadCount, 0)

  return (
    <div style={containerStyle}>
      {inboxOpen && activeUserId && (
        <ChatWindow userId={activeUserId} onClose={closeChat} />
      )}

      {inboxOpen && (
        <InboxPanel onSelect={openChat} onClose={closeInbox} selectedUserId={activeUserId} />
      )}

      <button onClick={toggleInbox} style={toggleButtonStyle} aria-label={t('messages.title')}>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
        </svg>
        {totalUnread > 0 && (
          <span style={toggleBadgeStyle}>{totalUnread > 99 ? '99+' : totalUnread}</span>
        )}
      </button>
    </div>
  )
}
