import { client } from './client'
import type { AuthResponse, EmailLanguage, RegisterResponse } from '../types'

export async function login(email: string, password: string): Promise<AuthResponse> {
  const response = await client.post<AuthResponse>('/auth/login', { email, password })
  return response.data
}

export async function register(
  email: string,
  password: string,
  displayName: string,
  language: EmailLanguage,
): Promise<RegisterResponse> {
  const response = await client.post<RegisterResponse>('/auth/register', {
    email,
    password,
    displayName,
    language,
  })
  return response.data
}

export async function logout(): Promise<void> {
  await client.post('/auth/logout')
}

export async function forgotPassword(email: string, language: EmailLanguage): Promise<void> {
  await client.post('/auth/forgot-password', { email, language })
}

export async function resetPassword(
  email: string,
  token: string,
  newPassword: string,
): Promise<void> {
  await client.post('/auth/reset-password', { email, token, newPassword })
}

export async function confirmEmail(email: string, token: string): Promise<void> {
  await client.post('/auth/confirm-email', { email, token })
}

/** The API answers the same whether or not the account exists, so success does not mean an email was sent. */
export async function resendConfirmation(email: string, language: EmailLanguage): Promise<void> {
  await client.post('/auth/resend-confirmation', { email, language })
}
