import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Bell } from 'lucide-react'
import { useChat } from '../contexts/ChatContext'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getNotifications,
  markNotificationRead,
  markAllNotificationsRead,
} from '../api/notificationsApi'
import type { NotificationItem } from '../types'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { ScrollArea } from '@/components/ui/scroll-area'

function relativeTime(iso: string, t: (key: string, opts?: Record<string, unknown>) => string): string {
  const diffMs = Date.now() - new Date(iso).getTime()
  const minutes = Math.floor(diffMs / 60000)
  if (minutes < 1) return t('notifications.justNow')
  if (minutes < 60) return t('notifications.minutesAgo', { count: minutes })
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return t('notifications.hoursAgo', { count: hours })
  const days = Math.floor(hours / 24)
  return t('notifications.daysAgo', { count: days })
}

function describe(item: NotificationItem, t: (key: string, opts?: Record<string, unknown>) => string): string {
  switch (item.type) {
    case 'BottleLiked':
      return item.resourceName
        ? t('notifications.bottleLiked', { bottle: item.resourceName })
        : t('notifications.bottleLikedNoName')
    case 'BottleCommented':
      return item.resourceName
        ? t('notifications.bottleCommented', { bottle: item.resourceName })
        : t('notifications.bottleCommentedNoName')
    case 'BottleReviewed':
      return item.resourceName
        ? t('notifications.bottleReviewed', { bottle: item.resourceName })
        : t('notifications.bottleReviewedNoName')
    case 'NewFollower':
      return t('notifications.newFollower')
    case 'NewMessage':
      return t('notifications.newMessage')
    case 'NewBottleFromFollowing':
      return item.resourceName
        ? t('notifications.newBottleFromFollowing', { bottle: item.resourceName })
        : t('notifications.newBottleFromFollowingNoName')
    case 'BottleListedForSale':
      return item.resourceName
        ? t('notifications.bottleListedForSale', { bottle: item.resourceName })
        : t('notifications.bottleListedForSaleNoName')
    case 'WishListMatch':
      return item.resourceName
        ? t('notifications.wishListMatch', { bottle: item.resourceName })
        : t('notifications.wishListMatchNoName')
    case 'OfferReceived':
      return item.resourceName
        ? t('notifications.offerReceived', { bottle: item.resourceName })
        : t('notifications.offerReceivedNoName')
    case 'OfferAccepted':
      return item.resourceName
        ? t('notifications.offerAccepted', { bottle: item.resourceName })
        : t('notifications.offerAcceptedNoName')
    case 'OfferDeclined':
      return item.resourceName
        ? t('notifications.offerDeclined', { bottle: item.resourceName })
        : t('notifications.offerDeclinedNoName')
    case 'BadgeEarned': {
      const name = item.resourceName
        ? t(`badges.${item.resourceName}.name`, { defaultValue: item.resourceName })
        : ''
      return name
        ? `${t('notifications.badgeEarned')} ${name}`
        : t('notifications.badgeEarned')
    }
  }
}

function targetPath(item: NotificationItem): string | null {
  switch (item.type) {
    case 'BottleLiked':
    case 'BottleCommented':
    case 'BottleReviewed':
    case 'NewFollower':
    case 'NewBottleFromFollowing':
    case 'BottleListedForSale':
      return `/bar/${item.actorId}`
    case 'NewMessage':
      return null
    case 'WishListMatch':
      return '/marketplace'
    case 'OfferReceived':
    case 'OfferAccepted':
    case 'OfferDeclined':
      return '/offers'
    case 'BadgeEarned':
      return '/profile'
  }
}

export default function NotificationBell() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { openChat } = useChat()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)

  const { data: summary } = useQuery({
    queryKey: ['notifications'],
    queryFn: getNotifications,
    refetchInterval: 30_000,
  })

  const readMutation = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  const readAllMutation = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  const unreadCount = summary?.unreadCount ?? 0
  const notifications = (summary?.notifications ?? []).slice(0, 30)

  function handleItemClick(item: NotificationItem) {
    if (!item.isRead) readMutation.mutate(item.id)
    setOpen(false)
    if (item.type === 'NewMessage') {
      openChat(item.actorId)
      return
    }
    const path = targetPath(item)
    if (path) navigate(path)
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={t('notifications.title')}
        >
          <Bell className="size-5 text-primary" />
          {unreadCount > 0 && (
            <Badge className="absolute -top-1 -right-1 h-4 min-w-4 justify-center rounded-full px-1 text-[10px]">
              {unreadCount > 99 ? '99+' : unreadCount}
            </Badge>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[340px] gap-0 p-0">
        <div className="flex items-center justify-between border-b border-border px-4 py-3">
          <span className="text-sm font-medium text-primary">{t('notifications.title')}</span>
          {unreadCount > 0 && (
            <Button
              variant="link"
              size="sm"
              onClick={() => readAllMutation.mutate()}
              disabled={readAllMutation.isPending}
            >
              {t('notifications.markAllRead')}
            </Button>
          )}
        </div>

        {notifications.length === 0 ? (
          <div className="px-4 py-7 text-center text-sm text-muted-foreground">
            {t('notifications.empty')}
          </div>
        ) : (
          <ScrollArea className="[&>[data-slot=scroll-area-viewport]]:max-h-[420px]">
            <div className="w-full">
            {notifications.map((item) => (
              <button
                key={item.id}
                onClick={() => handleItemClick(item)}
                className={cn(
                  'block w-full border-b border-border px-4 py-3 text-left',
                  item.isRead ? 'bg-transparent' : 'bg-accent',
                )}
              >
                <div className="text-sm leading-snug text-foreground">
                  {item.type !== 'BadgeEarned' && (
                    <>
                      <span className="font-medium text-primary">{item.actorDisplayName}</span>{' '}
                    </>
                  )}
                  <span>{describe(item, t)}</span>
                </div>
                <div className="mt-1 text-xs text-muted-foreground">
                  {relativeTime(item.createdAt, t)}
                </div>
              </button>
            ))}
            </div>
          </ScrollArea>
        )}
      </PopoverContent>
    </Popover>
  )
}
