import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getNotifications } from '@/api/notificationsApi'
import { acceptOffer, cancelDeal, confirmReceipt, getReceivedOffers, getSentOffers } from '@/api/offersApi'
import bg from '@/i18n/bg.json'
import OffersPage from '@/pages/OffersPage'
import { offer } from '../fixtures/offers'
import { apiError, renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/messagesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/offersApi')

/** Opens "ОФЕРТИ" as a logged-in collector; the received tab shows first. */
function renderOffers() {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  return renderWithProviders(<OffersPage />, { route: '/offers' })
}

describe('OffersPage', () => {
  beforeEach(() => {
    vi.mocked(getReceivedOffers).mockResolvedValue([])
    vi.mocked(getSentOffers).mockResolvedValue([])
  })

  it('shows the seller what accepting does, and accepts the offer', async () => {
    const pending = offer()
    vi.mocked(getReceivedOffers).mockResolvedValue([pending])
    vi.mocked(acceptOffer).mockResolvedValue({ ...pending, status: 'AwaitingReceipt' })
    renderOffers()

    expect(await screen.findByText(bg.offers.acceptHint)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: bg.offers.accept }))

    expect(acceptOffer).toHaveBeenCalledWith(pending.id)
  })

  /** Every bottle reserved for buyers (#130): the API refuses with a reason, and the page says why. */
  it('says that no bottle is left when the API refuses the accept for that reason', async () => {
    vi.mocked(getReceivedOffers).mockResolvedValue([offer()])
    vi.mocked(acceptOffer).mockRejectedValue(
      apiError(409, { error: 'No bottle left to sell.', reason: 'NoBottleLeft' }),
    )
    renderOffers()

    await userEvent.click(await screen.findByRole('button', { name: bg.offers.accept }))

    expect(await screen.findByRole('alert')).toHaveTextContent(bg.offers.errorNoBottleLeft)
  })

  it('shows the generic text for any other refusal', async () => {
    vi.mocked(getReceivedOffers).mockResolvedValue([offer()])
    vi.mocked(acceptOffer).mockRejectedValue(apiError(409, { error: 'Only pending offers can be accepted.' }))
    renderOffers()

    await userEvent.click(await screen.findByRole('button', { name: bg.offers.accept }))

    expect(await screen.findByRole('alert')).toHaveTextContent(bg.offers.errorRespond)
  })

  it('lets the seller cancel a deal that waits for the buyer', async () => {
    const waiting = offer({ status: 'AwaitingReceipt', respondedAt: '2026-11-02T10:00:00Z' })
    vi.mocked(getReceivedOffers).mockResolvedValue([waiting])
    vi.mocked(cancelDeal).mockResolvedValue({ ...waiting, status: 'Cancelled' })
    renderOffers()

    expect(await screen.findByText(bg.offers.statusAwaitingReceipt)).toBeInTheDocument()
    expect(screen.getByText(bg.offers.awaitingSeller)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: bg.offers.confirmReceipt })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: bg.offers.cancelDeal }))

    expect(cancelDeal).toHaveBeenCalledWith(waiting.id)
  })

  it('lets the buyer confirm receiving the bottle, or cancel the deal', async () => {
    const waiting = offer({ status: 'AwaitingReceipt' })
    vi.mocked(getSentOffers).mockResolvedValue([waiting])
    vi.mocked(confirmReceipt).mockResolvedValue({ ...waiting, status: 'Accepted' })
    renderOffers()

    await userEvent.click(screen.getByRole('tab', { name: bg.offers.tabSent }))
    expect(await screen.findByText(bg.offers.awaitingBuyer)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: bg.offers.cancelDeal })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: bg.offers.confirmReceipt }))

    await waitFor(() => expect(confirmReceipt).toHaveBeenCalledWith(waiting.id))
  })

  it('labels a completed deal and a cancelled one, with nothing left to do', async () => {
    vi.mocked(getReceivedOffers).mockResolvedValue([
      offer({ status: 'Accepted', bottleName: 'Completed bottle' }),
      offer({ status: 'Cancelled', bottleName: 'Cancelled bottle' }),
    ])
    renderOffers()

    expect(await screen.findByText(bg.offers.statusAccepted)).toBeInTheDocument()
    expect(screen.getByText(bg.offers.statusCancelled)).toBeInTheDocument()
    for (const action of [bg.offers.accept, bg.offers.decline, bg.offers.cancelDeal, bg.offers.confirmReceipt]) {
      expect(screen.queryByRole('button', { name: action })).not.toBeInTheDocument()
    }
  })
})
