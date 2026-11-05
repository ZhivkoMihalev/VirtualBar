import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { getMarketplace } from '@/api/bottlesApi'
import { getNotifications } from '@/api/notificationsApi'
import { getAllWishListItems } from '@/api/wishListApi'
import bg from '@/i18n/bg.json'
import MarketplacePage from '@/pages/MarketplacePage'
import { renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/bottlesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/wishListApi')

describe('MarketplacePage', () => {
  /**
   * A visitor opening the "Търсене" tab was sent to /login by the 401 of the wish list request (#100). The marketplace
   * is for logged-in collectors: a visitor sees a login gate, and the page sends none of the requests that answer 401.
   */
  it('shows a visitor the login gate and loads neither listings nor wishes (#100)', () => {
    renderWithProviders(<MarketplacePage />, { route: '/marketplace' })

    expect(screen.getByRole('heading', { name: bg.marketplace.loginGateTitle })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: bg.marketplace.loginGateLogin })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: bg.marketplace.loginGateRegister })).toHaveAttribute('href', '/register')
    expect(screen.queryByRole('tab', { name: bg.marketplace.tabSearch })).not.toBeInTheDocument()
    expect(getMarketplace).not.toHaveBeenCalled()
    expect(getAllWishListItems).not.toHaveBeenCalled()
  })

  it('shows a logged-in collector the tabs and the listings, and no login gate', async () => {
    signIn()
    vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
    vi.mocked(getMarketplace).mockResolvedValue([])

    renderWithProviders(<MarketplacePage />, { route: '/marketplace' })

    expect(await screen.findByText(bg.marketplace.empty)).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: bg.marketplace.tabSearch })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: bg.marketplace.loginGateTitle })).not.toBeInTheDocument()
    expect(getMarketplace).toHaveBeenCalled()
  })
})
