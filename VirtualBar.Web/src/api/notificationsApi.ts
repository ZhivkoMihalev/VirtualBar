import { client } from './client'
import type { NotificationSummary } from '../types'

export async function getNotifications(): Promise<NotificationSummary> {
  const res = await client.get<NotificationSummary>('/notifications')
  return res.data
}

export async function markNotificationRead(id: string): Promise<void> {
  await client.post(`/notifications/${id}/read`)
}

export async function markAllNotificationsRead(): Promise<void> {
  await client.post('/notifications/read-all')
}
