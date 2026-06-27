import { client } from './client'
import type { AuthResponse } from '../types'

export async function login(email: string, password: string): Promise<AuthResponse> {
  const response = await client.post<AuthResponse>('/auth/login', { email, password })
  return response.data
}

export async function register(
  email: string,
  password: string,
  displayName: string,
): Promise<AuthResponse> {
  const response = await client.post<AuthResponse>('/auth/register', {
    email,
    password,
    displayName,
  })
  return response.data
}

export async function logout(): Promise<void> {
  await client.post('/auth/logout')
}

export async function forgotPassword(email: string, language: string): Promise<void> {
  await client.post('/auth/forgot-password', { email, language })
}

export async function resetPassword(
  email: string,
  token: string,
  newPassword: string,
): Promise<void> {
  await client.post('/auth/reset-password', { email, token, newPassword })
}
