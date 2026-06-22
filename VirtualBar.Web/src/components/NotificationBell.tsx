import { useState, useEffect, useRef } from 'react'
import type { CSSProperties } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getNotifications,
  markNotificationRead,
  markAllNotificationsRead,
} from '../api/notificationsApi'
import type { NotificationItem } from '../types'

const bellButtonStyle: CSSProperties = {
  background: 'transparent',
  border: 'none',
  cursor: 'pointer',
  padding: 4,
  display: 'flex',
  alignItems: 'center',
  position: 'relative',
  color: '#C9A84C',
}

const badgeStyle: CSSProperties = {
  position: 'absolute',
  top: -2,
  right: -2,
  minWidth: 16,
  height: 16,
  padding: '0 4px',
  borderRadius: 8,
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  color: '#07030A',
  fontFamily: 'Cinzel, serif',
  fontSize: 9,
  fontWeight: 700,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  lineHeight: 1,
}

const dropdownStyle: CSSProperties = {
  position: 'absolute',
  right: 0,
  top: '100%',
  marginTop: 8,
  width: 340,
  maxHeight: 480,
  overflowY: 'auto',
  background: 'rgba(7,3,10,0.97)',
  border: '1px solid rgba(201,168,76,0.2)',
  borderRadius: 6,
  boxShadow: '0 8px 32px rgba(0,0,0,0.6)',
  zIndex: 100,
}

const dropdownHeaderStyle: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  padding: '12px 16px',
  borderBottom: '1px solid rgba(201,168,76,0.12)',
  position: 'sticky',
  top: 0,
  background: 'rgba(7,3,10,0.97)',
}

const dropdownTitleStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 12,
  letterSpacing: '0.15em',
  color: '#E8C870',
}

const markAllButtonStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.1em',
  color: '#B09868',
  background: 'transparent',
  border: 'none',
  cursor: 'pointer',
  padding: 0,
}

const emptyStateStyle: CSSProperties = {
  padding: '28px 16px',
  textAlign: 'center',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 15,
  color: '#8A7A5A',
}

const itemTextStyle: CSSProperties = {
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 15,
  color: '#E8E0D0',
  lineHeight: 1.4,
}

const itemTimeStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 9,
  letterSpacing: '0.1em',
  color: '#6A5C44',
  marginTop: 4,
}

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
  }
}

function targetPath(item: NotificationItem): string {
  switch (item.type) {
    case 'BottleLiked':
    case 'BottleCommented':
    case 'NewFollower':
    case 'NewBottleFromFollowing':
    case 'BottleListedForSale':
      return `/bar/${item.actorId}`
    case 'NewMessage':
      return `/messages?with=${item.actorId}`
  }
}

export default function NotificationBell() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

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

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const unreadCount = summary?.unreadCount ?? 0
  const notifications = (summary?.notifications ?? []).slice(0, 30)

  function handleItemClick(item: NotificationItem) {
    if (!item.isRead) readMutation.mutate(item.id)
    setOpen(false)
    navigate(targetPath(item))
  }

  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button
        onClick={() => setOpen(o => !o)}
        aria-label={t('notifications.title')}
        style={bellButtonStyle}
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        {unreadCount > 0 && (
          <span style={badgeStyle}>
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div style={dropdownStyle}>
          <div style={dropdownHeaderStyle}>
            <span style={dropdownTitleStyle}>
              {t('notifications.title')}
            </span>
            {unreadCount > 0 && (
              <button
                onClick={() => readAllMutation.mutate()}
                disabled={readAllMutation.isPending}
                style={markAllButtonStyle}
              >
                {t('notifications.markAllRead')}
              </button>
            )}
          </div>

          {notifications.length === 0 ? (
            <div style={emptyStateStyle}>
              {t('notifications.empty')}
            </div>
          ) : (
            notifications.map(item => (
              <button
                key={item.id}
                onClick={() => handleItemClick(item)}
                style={{
                  display: 'block',
                  width: '100%',
                  textAlign: 'left',
                  padding: '12px 16px',
                  border: 'none',
                  borderBottom: '1px solid rgba(201,168,76,0.08)',
                  cursor: 'pointer',
                  background: item.isRead ? 'rgba(255,255,255,0.02)' : 'rgba(201,168,76,0.08)',
                }}
              >
                <div style={itemTextStyle}>
                  <span style={{ color: '#E8C870', fontWeight: 600 }}>{item.actorDisplayName}</span>{' '}
                  <span>{describe(item, t)}</span>
                </div>
                <div style={itemTimeStyle}>
                  {relativeTime(item.createdAt, t)}
                </div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
