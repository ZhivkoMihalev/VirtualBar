import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { resendConfirmation } from '@/api/authApi'
import ResendConfirmation from '@/components/ResendConfirmation'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import en from '@/i18n/en.json'
import { deferred, networkError, renderWithProviders } from '../testUtils'

vi.mock('@/api/authApi')

const EMAIL = 'collector@example.com'

function renderResend() {
  renderWithProviders(<ResendConfirmation email={EMAIL} />)
  return screen.getByRole('button', { name: bg.confirmEmail.resendBtn })
}

describe('ResendConfirmation', () => {
  afterEach(async () => {
    await i18n.changeLanguage('bg')
  })

  it('sends a new link and says so, naming the email', async () => {
    vi.mocked(resendConfirmation).mockResolvedValue()
    const button = renderResend()

    await userEvent.click(button)

    expect(await screen.findByRole('status')).toHaveTextContent(i18n.t('confirmEmail.resendSuccess', { email: EMAIL }))
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(resendConfirmation).toHaveBeenCalledExactlyOnceWith(EMAIL, 'bg')
  })

  it('disables the button while the request is pending', async () => {
    const request = deferred<void>()
    vi.mocked(resendConfirmation).mockReturnValue(request.promise)
    const button = renderResend()

    await userEvent.click(button)

    expect(await screen.findByRole('button', { name: bg.confirmEmail.resendingBtn })).toBeDisabled()
    request.resolve()
    expect(await screen.findByRole('status')).toBeInTheDocument()
  })

  it('shows the failure and lets the user try again', async () => {
    vi.mocked(resendConfirmation).mockRejectedValueOnce(networkError()).mockResolvedValueOnce()
    const button = renderResend()

    await userEvent.click(button)

    expect(await screen.findByText(bg.confirmEmail.resendFailed)).toBeInTheDocument()
    const retry = screen.getByRole('button', { name: bg.confirmEmail.resendBtn })
    expect(retry).toBeEnabled()

    await userEvent.click(retry)

    expect(await screen.findByRole('status')).toBeInTheDocument()
    expect(resendConfirmation).toHaveBeenCalledTimes(2)
  })

  it('asks for the email in English when the UI is in English', async () => {
    await i18n.changeLanguage('en')
    vi.mocked(resendConfirmation).mockResolvedValue()
    renderWithProviders(<ResendConfirmation email={EMAIL} />)

    await userEvent.click(screen.getByRole('button', { name: en.confirmEmail.resendBtn }))

    expect(await screen.findByRole('status')).toBeInTheDocument()
    expect(resendConfirmation).toHaveBeenCalledExactlyOnceWith(EMAIL, 'en')
  })
})
