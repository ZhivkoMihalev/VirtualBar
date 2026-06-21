import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import { getInbox, getConversation, sendMessage, markRead } from '../api/messagesApi'
import type { ConversationSummary, Message } from '../types'
import NavBar from '../components/NavBar'

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

function initials(name: string): string {
  return name.trim().charAt(0).toUpperCase() || '?'
}

function Avatar({ name, url, size }: { name: string; url?: string; size: number }) {
  if (url) {
    return (
      <img
        src={url}
        alt={name}
        style={{
          width: size,
          height: size,
          borderRadius: '50%',
          objectFit: 'cover',
          border: '2px solid rgba(201,168,76,0.4)',
          flexShrink: 0,
        }}
      />
    )
  }

  return (
    <div
      style={{
        width: size,
        height: size,
        borderRadius: '50%',
        border: '2px solid rgba(201,168,76,0.4)',
        background: 'radial-gradient(ellipse at 50% 30%, rgba(201,168,76,0.15), rgba(10,5,2,0.6))',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'Playfair Display, serif',
        fontSize: size * 0.42,
        color: '#E8C870',
        flexShrink: 0,
      }}
    >
      {initials(name)}
    </div>
  )
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
    conversation.lastMessageContent.length > 50
      ? `${conversation.lastMessageContent.slice(0, 50)}…`
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
        padding: '14px 16px',
        background: selected ? 'rgba(201,168,76,0.08)' : 'transparent',
        borderLeft: selected ? '3px solid #C9A84C' : '3px solid transparent',
        borderTop: 'none',
        borderRight: 'none',
        borderBottom: '1px solid rgba(201,168,76,0.08)',
        cursor: 'pointer',
      }}
    >
      <Avatar name={conversation.otherUserDisplayName} url={conversation.otherUserAvatarUrl} size={44} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 8 }}>
          <span
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontSize: 17,
              color: '#E8C870',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {conversation.otherUserDisplayName}
          </span>
          <span style={{ fontFamily: 'Cinzel, serif', fontSize: 10, color: '#806840', flexShrink: 0 }}>
            {formatTime(conversation.lastMessageAt)}
          </span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, marginTop: 2 }}>
          <span
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontSize: 14,
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
                minWidth: 20,
                height: 20,
                borderRadius: '50%',
                background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                color: '#07030A',
                fontFamily: 'Cinzel, serif',
                fontSize: 11,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: '0 6px',
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
          maxWidth: '70%',
          padding: '10px 14px',
          borderRadius: 8,
          background: mine ? 'rgba(201,168,76,0.15)' : 'rgba(255,255,255,0.05)',
          border: mine ? '1px solid rgba(201,168,76,0.3)' : '1px solid rgba(255,255,255,0.08)',
        }}
      >
        <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, color: '#F0DDB4', lineHeight: 1.4, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
          {message.content}
        </div>
        <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.1em', color: '#806840', marginTop: 4, textAlign: 'right' }}>
          {formatTime(message.createdAt)}
        </div>
      </div>
    </div>
  )
}

export default function MessagesPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [draft, setDraft] = useState('')
  const scrollRef = useRef<HTMLDivElement>(null)

  const withParam = searchParams.get('with')

  useEffect(() => {
    if (withParam) {
      setSelectedUserId(withParam)
    }
  }, [withParam])

  const {
    data: inbox = [],
    isLoading: inboxLoading,
    isError: inboxError,
  } = useQuery({
    queryKey: ['inbox'],
    queryFn: getInbox,
  })

  const {
    data: thread = [],
    isLoading: threadLoading,
    isError: threadError,
  } = useQuery({
    queryKey: ['conversation', selectedUserId],
    queryFn: () => getConversation(selectedUserId!),
    enabled: !!selectedUserId,
  })

  const markReadMutation = useMutation({
    mutationFn: markRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
    },
  })

  const sendMutation = useMutation({
    mutationFn: (content: string) => sendMessage(selectedUserId!, content),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
      queryClient.invalidateQueries({ queryKey: ['conversation', selectedUserId] })
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

  const selectedConversation = inbox.find(c => c.otherUserId === selectedUserId)
  const headerName =
    selectedConversation?.otherUserDisplayName ??
    thread.find(m => m.senderId === selectedUserId)?.senderDisplayName ??
    ''

  const handleSend = () => {
    const content = draft.trim()
    if (!content || !selectedUserId || sendMutation.isPending) {
      return
    }
    sendMutation.mutate(content)
    setDraft('')
  }

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4', display: 'flex', flexDirection: 'column' }}>
      <NavBar />

      <div style={{ flex: 1, display: 'flex', minHeight: 0 }}>
        <aside
          style={{
            width: 320,
            flexShrink: 0,
            background: 'rgba(7,3,10,0.92)',
            borderTop: '2px solid rgba(201,168,76,0.4)',
            borderRight: '1px solid rgba(201,168,76,0.12)',
            overflowY: 'auto',
          }}
        >
          <div
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 13,
              letterSpacing: '0.3em',
              color: '#C9A84C',
              padding: '20px 16px 16px',
              borderBottom: '1px solid rgba(201,168,76,0.12)',
            }}
          >
            {t('messages.title')}
          </div>

          {inboxLoading && (
            <div style={{ padding: '40px 16px', textAlign: 'center', fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.3em', color: '#C9A84C' }}>
              {t('messages.loading')}
            </div>
          )}

          {inboxError && !inboxLoading && (
            <div style={{ padding: '40px 16px', textAlign: 'center', fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#C04040' }}>
              {t('messages.error')}
            </div>
          )}

          {!inboxLoading && !inboxError && inbox.length === 0 && (
            <div style={{ padding: '40px 16px', textAlign: 'center', fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#B09868' }}>
              {t('messages.noConversations')}
            </div>
          )}

          {!inboxLoading && !inboxError &&
            inbox.map(conversation => (
              <ConversationCard
                key={conversation.otherUserId}
                conversation={conversation}
                selected={conversation.otherUserId === selectedUserId}
                you={t('messages.you')}
                onSelect={setSelectedUserId}
              />
            ))}
        </aside>

        <section style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0, borderTop: '2px solid rgba(201,168,76,0.4)' }}>
          {!selectedUserId && (
            <div
              style={{
                flex: 1,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 20,
                fontStyle: 'italic',
                color: '#B09868',
                padding: 40,
                textAlign: 'center',
              }}
            >
              {t('messages.selectConversation')}
            </div>
          )}

          {selectedUserId && (
            <>
              <div
                style={{
                  padding: '18px 24px',
                  borderBottom: '1px solid rgba(201,168,76,0.12)',
                  fontFamily: 'Playfair Display, serif',
                  fontSize: 22,
                  color: '#E8C870',
                }}
              >
                {headerName}
              </div>

              <div ref={scrollRef} style={{ flex: 1, overflowY: 'auto', padding: '24px', display: 'flex', flexDirection: 'column', gap: 12 }}>
                {threadLoading && (
                  <div style={{ textAlign: 'center', fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.3em', color: '#C9A84C', padding: 40 }}>
                    {t('messages.loading')}
                  </div>
                )}

                {threadError && !threadLoading && (
                  <div style={{ textAlign: 'center', fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#C04040', padding: 40 }}>
                    {t('messages.error')}
                  </div>
                )}

                {!threadLoading && !threadError &&
                  thread.map(message => (
                    <MessageBubble key={message.id} message={message} mine={message.senderId === currentUserId} />
                  ))}
              </div>

              <div style={{ display: 'flex', gap: 12, padding: '16px 24px', borderTop: '1px solid rgba(201,168,76,0.12)' }}>
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
                  rows={2}
                  style={{
                    flex: 1,
                    resize: 'none',
                    background: 'rgba(255,255,255,0.04)',
                    border: '1px solid rgba(201,168,76,0.2)',
                    borderRadius: 4,
                    padding: '10px 14px',
                    color: '#F0DDB4',
                    fontFamily: 'Cormorant Garamond, serif',
                    fontSize: 16,
                    outline: 'none',
                  }}
                />
                <button
                  onClick={handleSend}
                  disabled={!draft.trim() || sendMutation.isPending}
                  style={{
                    fontFamily: 'Cinzel, serif',
                    fontSize: 11,
                    letterSpacing: '0.2em',
                    padding: '0 24px',
                    borderRadius: 2,
                    cursor: !draft.trim() || sendMutation.isPending ? 'not-allowed' : 'pointer',
                    opacity: !draft.trim() || sendMutation.isPending ? 0.5 : 1,
                    color: '#07030A',
                    background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                    border: 'none',
                    flexShrink: 0,
                  }}
                >
                  {sendMutation.isPending ? t('messages.sending') : t('messages.send')}
                </button>
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  )
}
