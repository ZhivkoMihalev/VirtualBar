import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { getFeed } from '@/api/feedApi'
import { getNotifications } from '@/api/notificationsApi'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import HomePage from '@/pages/HomePage'
import type { FeedItem } from '@/types'
import { reviewedItem } from '../fixtures/feed'
import { renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/feedApi')
vi.mock('@/api/newsApi')
vi.mock('@/api/notificationsApi')

/** The home page of a logged-in collector whose feed holds `items`. */
function renderFeed(...items: FeedItem[]) {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  vi.mocked(getFeed).mockResolvedValue(items)
  return renderWithProviders(<HomePage />)
}

describe('HomePage — a followed collector rated a bottle (#57)', () => {
  it('shows the reviewer, the score, the summary and the flavors', async () => {
    const item = reviewedItem({
      actorDisplayName: 'Maria',
      reviewScore: 94,
      reviewSummary: 'Brine, smoke and a long sweet finish.',
      reviewFlavors: ['Smoky', 'Maritime', 'Honey'],
    })
    renderFeed(item)

    const reviewer = await screen.findByRole('link', { name: 'Maria' })
    expect(reviewer).toHaveAttribute('href', `/bar/${item.actorUserId}`)
    expect(screen.getByText(bg.home.rated)).toBeInTheDocument()
    expect(screen.getByText('94')).toBeInTheDocument()
    expect(screen.getByRole('blockquote')).toHaveTextContent('Brine, smoke and a long sweet finish.')
    expect(screen.getByText(bg.flavors.Smoky)).toBeInTheDocument()
    expect(screen.getByText(bg.flavors.Maritime)).toBeInTheDocument()
    expect(screen.getByText(bg.flavors.Honey)).toBeInTheDocument()
  })

  it('links a review of a catalog-linked bottle to the product page, under the product name and brand', async () => {
    const item = reviewedItem({
      bottleName: 'My Lagavulin',
      productId: 'product-1',
      productName: '16 Year Old',
      productBrand: 'Lagavulin',
    })
    renderFeed(item)

    const title = await screen.findByRole('link', { name: '16 Year Old' })
    expect(title).toHaveAttribute('href', '/product/product-1')
    expect(screen.getByText('Lagavulin')).toBeInTheDocument()
    expect(screen.queryByText('My Lagavulin')).not.toBeInTheDocument()
    expect(screen.queryByText(i18n.t('home.fromBarOf', { name: item.bottleUserDisplayName }))).not.toBeInTheDocument()
  })

  it("links a review of an unlinked bottle to the owner's bar, and names that bar", async () => {
    const item = reviewedItem({ bottleName: 'Old Pulteney 12', bottleUserDisplayName: 'Ivan' })
    renderFeed(item)

    const title = await screen.findByRole('link', { name: 'Old Pulteney 12' })
    expect(title).toHaveAttribute('href', `/bar/${item.bottleUserId}`)
    expect(screen.getByText(i18n.t('home.fromBarOf', { name: 'Ivan' }))).toBeInTheDocument()
  })

  it('does not name the bar when collectors rated a bottle of their own', async () => {
    const item = reviewedItem({ actorDisplayName: 'Ivan', bottleUserDisplayName: 'Ivan' })
    item.bottleUserId = item.actorUserId!
    renderFeed(item)

    expect(await screen.findByRole('link', { name: item.bottleName })).toHaveAttribute('href', `/bar/${item.actorUserId}`)
    expect(screen.queryByText(i18n.t('home.fromBarOf', { name: 'Ivan' }))).not.toBeInTheDocument()
  })

  it('shows no summary when the review has none', async () => {
    renderFeed(reviewedItem({ reviewSummary: null }))

    await screen.findByText(bg.home.rated)
    expect(screen.queryByRole('blockquote')).not.toBeInTheDocument()
  })
})
