import { describe, expect, it } from 'vitest'
import bg from '@/i18n/bg.json'
import en from '@/i18n/en.json'
import { authErrorMessage, authErrorReason } from '@/lib/authErrors'
import type { AuthErrorReason } from '@/types'
import { apiError, networkError } from '../testUtils'

const REASONS: AuthErrorReason[] = ['InvalidCredentials', 'LockedOut', 'EmailNotConfirmed', 'EmailTaken']

const t = (key: string) => `t(${key})`

describe('authErrorReason', () => {
  it.each(REASONS)('returns %s when the API sends it', reason => {
    expect(authErrorReason(apiError(400, { error: 'English text', reason }))).toBe(reason)
  })

  it('ignores a reason this app does not know', () => {
    expect(authErrorReason(apiError(400, { reason: 'SomethingNew' }))).toBeUndefined()
  })

  it('ignores a reason that is only an inherited property name', () => {
    expect(authErrorReason(apiError(400, { reason: 'toString' }))).toBeUndefined()
  })

  it('ignores a reason that is not a string', () => {
    expect(authErrorReason({ response: { data: { reason: 42 } } })).toBeUndefined()
  })

  it('returns nothing when the body has no reason', () => {
    expect(authErrorReason(apiError(500, { error: 'Server error' }))).toBeUndefined()
  })

  it('returns nothing for a network error with no answer', () => {
    expect(authErrorReason(networkError())).toBeUndefined()
  })

  it.each([null, undefined])('returns nothing for %s', error => {
    expect(authErrorReason(error)).toBeUndefined()
  })
})

describe('authErrorMessage', () => {
  it.each(REASONS)('translates %s from the authErrors namespace', reason => {
    expect(authErrorMessage(apiError(400, { reason }), t, 'login.errorFailed')).toBe(`t(authErrors.${reason})`)
  })

  it('falls back to the form message for an unknown reason, never the English error text', () => {
    const error = apiError(400, { error: 'Some English text', reason: 'SomethingNew' })
    expect(authErrorMessage(error, t, 'login.errorFailed')).toBe('t(login.errorFailed)')
  })

  it('falls back to the form message for a network error', () => {
    expect(authErrorMessage(networkError(), t, 'register.errorFailed')).toBe('t(register.errorFailed)')
  })

  it.each(REASONS)('has a translation for %s in both languages', reason => {
    expect(bg.authErrors).toHaveProperty(reason)
    expect(en.authErrors).toHaveProperty(reason)
  })
})
