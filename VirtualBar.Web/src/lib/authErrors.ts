import type { AxiosError } from 'axios'
import type { ApiErrorBody, AuthErrorReason } from '../types'
import type { TFn } from './validation'

// A record rather than a list, so the compiler insists on every reason AuthErrorReason names.
const KNOWN_REASONS: Record<AuthErrorReason, true> = {
  InvalidCredentials: true,
  LockedOut: true,
  EmailNotConfirmed: true,
  EmailTaken: true,
}

function isAuthErrorReason(value: unknown): value is AuthErrorReason {
  return typeof value === 'string' && Object.hasOwn(KNOWN_REASONS, value)
}

/** The `reason` of a failed auth request, when the API sent one this app knows. */
export function authErrorReason(error: unknown): AuthErrorReason | undefined {
  const reason = (error as AxiosError<ApiErrorBody> | null | undefined)?.response?.data?.reason
  return isAuthErrorReason(reason) ? reason : undefined
}

/**
 * The message a failed login or registration shows. The API's own `error` text is English, so it is never shown
 * as is: a known `reason` maps to its translation (`authErrors.<reason>`), anything else, including a network
 * error with no response at all, to the form's generic message.
 */
export function authErrorMessage(error: unknown, t: TFn, fallbackKey: string): string {
  const reason = authErrorReason(error)
  return t(reason ? `authErrors.${reason}` : fallbackKey)
}
