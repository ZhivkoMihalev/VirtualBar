import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { confirmReceipt, getSentOffers } from '@/api/offersApi'
import ReceiptReminder, { RECEIPT_REMINDER_KEY } from '@/components/ReceiptReminder'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import { offer } from '../fixtures/offers'
import { renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/offersApi')

describe('ReceiptReminder', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('renders nothing for a visitor and asks the API nothing', () => {
    renderWithProviders(<ReceiptReminder />)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(getSentOffers).not.toHaveBeenCalled()
  })

  it('renders nothing when no purchase waits for the buyer', async () => {
    signIn()
    vi.mocked(getSentOffers).mockResolvedValue([offer({ status: 'Pending' }), offer({ status: 'Accepted' })])
    renderWithProviders(<ReceiptReminder />)

    await waitFor(() => expect(getSentOffers).toHaveBeenCalled())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  /** A buyer whose accepted offer waits for them is asked when they log in whether the bottle arrived (#130). */
  it('asks the buyer about each purchase that waits, and confirms the one they received', async () => {
    signIn()
    const waiting = offer({
      status: 'AwaitingReceipt',
      bottleName: 'Springbank 21',
      sellerDisplayName: 'Ivan',
      respondedAt: '2026-11-02T10:00:00Z',
    })
    vi.mocked(getSentOffers).mockResolvedValue([waiting, offer({ status: 'Pending', bottleName: 'Still pending' })])
    vi.mocked(confirmReceipt).mockResolvedValue({ ...waiting, status: 'Accepted' })
    renderWithProviders(<ReceiptReminder />)

    expect(await screen.findByRole('dialog', { name: bg.offers.reminderTitle })).toBeInTheDocument()
    expect(screen.getByText('Springbank 21')).toBeInTheDocument()
    expect(
      screen.getByText(
        i18n.t('offers.reminderFrom', { name: 'Ivan', date: new Date('2026-11-02T10:00:00Z').toLocaleDateString() }),
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText('Still pending')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: bg.offers.confirmReceipt }))

    await waitFor(() => expect(confirmReceipt).toHaveBeenCalledWith(waiting.id))
  })

  it('puts the reminder off until the next login with "Още не"', async () => {
    signIn()
    vi.mocked(getSentOffers).mockResolvedValue([offer({ status: 'AwaitingReceipt' })])
    const { unmount } = renderWithProviders(<ReceiptReminder />)

    await userEvent.click(await screen.findByRole('button', { name: bg.offers.reminderLater }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(sessionStorage.getItem(RECEIPT_REMINDER_KEY)).toBe('jwt-token')

    // The same session opening the app again: still put off.
    unmount()
    renderWithProviders(<ReceiptReminder />)
    await waitFor(() => expect(getSentOffers).toHaveBeenCalledTimes(2))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('asks again after a new login, whose session the reminder was not put off for', async () => {
    sessionStorage.setItem(RECEIPT_REMINDER_KEY, 'an-earlier-token')
    signIn()
    vi.mocked(getSentOffers).mockResolvedValue([offer({ status: 'AwaitingReceipt' })])
    renderWithProviders(<ReceiptReminder />)

    expect(await screen.findByRole('dialog', { name: bg.offers.reminderTitle })).toBeInTheDocument()
  })
})
