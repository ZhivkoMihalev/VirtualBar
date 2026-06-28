import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { MessageCircle, X } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { useChat } from '../contexts/ChatContext'
import { getInbox, getConversation, sendMessage, markRead } from '../api/messagesApi'
import type { ConversationSummary, Message } from '../types'
import { cn } from '@/lib/utils'
import Avatar from './Avatar'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Textarea } from '@/components/ui/textarea'
import { ScrollArea } from '@/components/ui/scroll-area'

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
      className={cn(
        'flex w-full cursor-pointer items-center gap-3 border-b border-l-[3px] border-border/50 px-3.5 py-3 text-left transition-colors',
        selected ? 'border-l-primary bg-accent' : 'border-l-transparent hover:bg-accent/50',
      )}
    >
      <Avatar
        displayName={conversation.otherUserDisplayName}
        avatarUrl={conversation.otherUserAvatarUrl}
        size={40}
      />
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline justify-between gap-2">
          <span className="truncate text-sm font-medium text-foreground">
            {conversation.otherUserDisplayName}
          </span>
          <span className="shrink-0 text-[10px] text-muted-foreground">
            {formatTime(conversation.lastMessageAt)}
          </span>
        </div>
        <div className="mt-0.5 flex items-center justify-between gap-2">
          <span className="truncate text-xs text-muted-foreground">
            {conversation.lastMessageIsFromMe ? `${you}: ${preview}` : preview}
          </span>
          {conversation.unreadCount > 0 && (
            <Badge className="min-w-5 shrink-0 justify-center rounded-full px-1.5">
              {conversation.unreadCount}
            </Badge>
          )}
        </div>
      </div>
    </button>
  )
}

function MessageBubble({ message, mine }: { message: Message; mine: boolean }) {
  return (
    <div className={cn('flex', mine ? 'justify-end' : 'justify-start')}>
      <div
        className={cn(
          'max-w-[75%] rounded-lg border px-3 py-2',
          mine ? 'border-primary/30 bg-primary/15' : 'border-border bg-muted',
        )}
      >
        <div className="whitespace-pre-wrap break-words text-sm leading-snug text-foreground">
          {message.content}
        </div>
        <div className="mt-1 text-right text-[10px] text-muted-foreground">
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
    <div className="pointer-events-auto flex h-[480px] w-[300px] flex-col overflow-hidden rounded-t-lg border border-b-0 border-border bg-popover shadow-2xl">
      <div className="flex shrink-0 items-center justify-between border-b border-border px-4 py-3.5">
        <span className="text-sm font-medium tracking-wide text-primary">{t('messages.title')}</span>
        <Button variant="ghost" size="icon-xs" onClick={onClose} aria-label={t('messages.close')}>
          <X className="size-4" />
        </Button>
      </div>

      <ScrollArea className="min-h-0 flex-1">
        <div className="w-full">
          {isLoading && (
            <div className="px-4 py-10 text-center text-xs tracking-wide text-primary">
              {t('messages.loading')}
            </div>
          )}

          {isError && !isLoading && (
            <div className="px-4 py-10 text-center text-sm italic text-destructive">
              {t('messages.error')}
            </div>
          )}

          {!isLoading && !isError && inbox.length === 0 && (
            <div className="px-4 py-10 text-center text-sm italic text-muted-foreground">
              {t('messages.noConversations')}
            </div>
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
      </ScrollArea>
    </div>
  )
}

function ChatWindow({ userId, onClose }: { userId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState('')
  const endRef = useRef<HTMLDivElement>(null)

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
    endRef.current?.scrollIntoView({ block: 'end' })
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
    <div className="pointer-events-auto flex h-[480px] w-80 flex-col overflow-hidden rounded-t-lg border border-b-0 border-border bg-popover shadow-2xl">
      <div className="flex shrink-0 items-center justify-between border-b border-border px-4 py-3.5">
        <span className="truncate font-heading text-base font-medium text-foreground">{headerName}</span>
        <Button variant="ghost" size="icon-xs" onClick={onClose} aria-label={t('messages.close')}>
          <X className="size-4" />
        </Button>
      </div>

      <ScrollArea className="min-h-0 flex-1">
        <div className="flex w-full flex-col gap-2.5 p-4">
          {isLoading && (
            <div className="px-4 py-10 text-center text-xs tracking-wide text-primary">
              {t('messages.loading')}
            </div>
          )}

          {isError && !isLoading && (
            <div className="px-4 py-10 text-center text-sm italic text-destructive">
              {t('messages.error')}
            </div>
          )}

          {!isLoading && !isError &&
            thread.map(message => (
              <MessageBubble
                key={message.id}
                message={message}
                mine={message.senderId === currentUserId}
              />
            ))}

          <div ref={endRef} />
        </div>
      </ScrollArea>

      <div className="flex shrink-0 items-end gap-2 border-t border-border px-3.5 py-3">
        <Textarea
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
          className="max-h-24 min-h-9 flex-1 resize-none"
        />
        <Button
          size="sm"
          onClick={handleSend}
          disabled={!draft.trim() || sendMutation.isPending}
          className="h-9 shrink-0"
        >
          {sendMutation.isPending ? t('messages.sending') : t('messages.send')}
        </Button>
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
    <div className="pointer-events-none fixed right-5 bottom-0 z-[1000] flex items-end gap-3">
      {inboxOpen && activeUserId && <ChatWindow userId={activeUserId} onClose={closeChat} />}

      {inboxOpen && (
        <InboxPanel onSelect={openChat} onClose={closeInbox} selectedUserId={activeUserId} />
      )}

      <Button
        size="icon"
        onClick={toggleInbox}
        aria-label={t('messages.title')}
        className="pointer-events-auto relative mb-5 size-[52px] rounded-full shadow-2xl"
      >
        {inboxOpen ? <X className="size-6" /> : <MessageCircle className="size-6" />}
        {totalUnread > 0 && (
          <Badge
            variant="destructive"
            className="pointer-events-none absolute -top-1 -right-1 min-w-5 justify-center rounded-full bg-destructive px-1 text-[10px] text-white dark:bg-destructive"
          >
            {totalUnread > 99 ? '99+' : totalUnread}
          </Badge>
        )}
      </Button>
    </div>
  )
}
