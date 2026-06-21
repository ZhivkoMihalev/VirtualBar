import { client } from './client'
import type { Message, ConversationSummary } from '../types'

export const getInbox = (): Promise<ConversationSummary[]> =>
  client.get('/api/messages').then(r => r.data)

export const getConversation = (userId: string): Promise<Message[]> =>
  client.get(`/api/messages/${userId}`).then(r => r.data)

export const sendMessage = (receiverId: string, content: string): Promise<Message> =>
  client.post('/api/messages', { receiverId, content }).then(r => r.data)

export const markRead = (messageId: string): Promise<void> =>
  client.post(`/api/messages/${messageId}/read`).then(r => r.data)
