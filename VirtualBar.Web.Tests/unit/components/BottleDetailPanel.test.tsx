import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getBottleComments } from '@/api/bottlesApi'
import { getNotifications } from '@/api/notificationsApi'
import { getBottleEstimate } from '@/api/pricesApi'
import { getReviews } from '@/api/reviewsApi'
import BottleDetailPanel from '@/components/BottleDetailPanel'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import type { Bottle } from '@/types'
import { bottle, noReviews } from '../fixtures/bottles'
import { renderWithProviders, signedInUser, signIn } from '../testUtils'

vi.mock('@/api/bottlesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/offersApi')
vi.mock('@/api/pricesApi')
vi.mock('@/api/reviewsApi')

/** Opens the panel on another collector's bottle, as a public bar does. */
function renderPanel(shown: Bottle, currentUserId = '') {
  return renderWithProviders(
    <BottleDetailPanel bottle={shown} userId={shown.userId} currentUserId={currentUserId} onClose={() => {}} />,
  )
}

/** Logs a collector in, with the NavBar's notifications and the price estimate answered. */
function signInCollector() {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  vi.mocked(getBottleEstimate).mockResolvedValue(null)
}

describe('BottleDetailPanel', () => {
  beforeEach(() => {
    vi.mocked(getBottleComments).mockResolvedValue([])
    vi.mocked(getReviews).mockResolvedValue(noReviews())
  })

  /** A visitor sees that the bottle is for sale and how many, but a login link instead of the price (#103). */
  it('shows a visitor a listed bottle without its price, with a link to log in', async () => {
    // The API sends a visitor no price.
    renderPanel(bottle({ isForSale: true, forSaleQuantity: 2, quantity: 3 }))

    expect(await screen.findByText(bg.bottle.forSale)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: bg.bottle.loginToSeePrice })).toHaveAttribute('href', '/login')
    expect(screen.getByText(i18n.t('bottle.availableForSale', { count: 2 }))).toBeInTheDocument()
    expect(screen.queryByText(bg.bottle.perBottle)).not.toBeInTheDocument()
  })

  it('shows a logged-in collector the price of a listed bottle, and no login link', async () => {
    signInCollector()
    renderPanel(
      bottle({ isForSale: true, forSaleQuantity: 2, quantity: 3, askingPrice: 150, currency: 'GBP' }),
      signedInUser.id,
    )

    expect(await screen.findByText(/GBP 150/)).toBeInTheDocument()
    expect(screen.getByText(bg.bottle.perBottle)).toBeInTheDocument()
    expect(screen.getByText(i18n.t('bottle.availableForSale', { count: 2 }))).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: bg.bottle.loginToSeePrice })).not.toBeInTheDocument()
  })

  it('shows a visitor no sale box for a bottle that is not for sale', async () => {
    renderPanel(bottle())

    expect(await screen.findByRole('heading', { name: 'Lagavulin 16' })).toBeInTheDocument()
    expect(screen.queryByText(bg.bottle.askingPrice)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: bg.bottle.loginToSeePrice })).not.toBeInTheDocument()
  })
})
