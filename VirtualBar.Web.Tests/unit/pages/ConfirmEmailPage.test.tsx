import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { confirmEmail } from '@/api/authApi'
import bg from '@/i18n/bg.json'
import ConfirmEmailPage from '@/pages/ConfirmEmailPage'
import { apiError, deferred, networkError, renderWithProviders } from '../testUtils'

vi.mock('@/api/authApi')

const EMAIL = 'collector@example.com'
const TOKEN = 'confirmation-token'
const LINK = `/confirm-email?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}`

function renderPage(route = LINK) {
  renderWithProviders(<ConfirmEmailPage />, { route })
}

describe('ConfirmEmailPage', () => {
  describe('a link without email or token', () => {
    it('says the link is invalid and sends no request', () => {
      renderPage('/confirm-email')

      expect(screen.getByText(bg.confirmEmail.invalidLink)).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: bg.confirmEmail.resendBtn })).not.toBeInTheDocument()
      expect(confirmEmail).not.toHaveBeenCalled()
    })

    it('offers a new link when the email is there but the token is not', () => {
      renderPage(`/confirm-email?email=${encodeURIComponent(EMAIL)}`)

      expect(screen.getByText(bg.confirmEmail.invalidLink)).toBeInTheDocument()
      expect(screen.getByRole('button', { name: bg.confirmEmail.resendBtn })).toBeInTheDocument()
      expect(confirmEmail).not.toHaveBeenCalled()
    })

    it('offers no new link when only the token is there', () => {
      renderPage(`/confirm-email?token=${TOKEN}`)

      expect(screen.getByText(bg.confirmEmail.invalidLink)).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: bg.confirmEmail.resendBtn })).not.toBeInTheDocument()
    })
  })

  it('shows that it is confirming while the request is pending', () => {
    vi.mocked(confirmEmail).mockReturnValue(deferred<void>().promise)

    renderPage()

    expect(screen.getByRole('status')).toHaveTextContent(bg.confirmEmail.confirming)
  })

  it('confirms the email once and links to the login page', async () => {
    vi.mocked(confirmEmail).mockResolvedValue()

    renderPage()

    expect(await screen.findByText(bg.confirmEmail.success)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: bg.confirmEmail.loginBtn })).toHaveAttribute('href', '/login')
    expect(confirmEmail).toHaveBeenCalledExactlyOnceWith(EMAIL, TOKEN)
  })

  it('says the link is invalid and offers a new one when the API answers 400', async () => {
    vi.mocked(confirmEmail).mockRejectedValue(apiError(400, { error: 'Invalid token.' }))

    renderPage()

    expect(await screen.findByText(bg.confirmEmail.invalidLink)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: bg.confirmEmail.resendBtn })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: bg.confirmEmail.retryBtn })).not.toBeInTheDocument()
  })

  it.each([
    ['a server error', apiError(500)],
    ['the rate limit', apiError(429)],
    ['no answer at all', networkError()],
  ])('offers to try the same link again after %s', async (_, error) => {
    vi.mocked(confirmEmail).mockRejectedValue(error)

    renderPage()

    expect(await screen.findByText(bg.confirmEmail.errorFailed)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: bg.confirmEmail.retryBtn })).toBeEnabled()
    expect(screen.queryByRole('button', { name: bg.confirmEmail.resendBtn })).not.toBeInTheDocument()
  })

  it('confirms the email when trying again succeeds', async () => {
    vi.mocked(confirmEmail).mockRejectedValueOnce(apiError(500)).mockResolvedValueOnce()
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: bg.confirmEmail.retryBtn }))

    expect(await screen.findByText(bg.confirmEmail.success)).toBeInTheDocument()
    expect(confirmEmail).toHaveBeenCalledTimes(2)
  })
})
