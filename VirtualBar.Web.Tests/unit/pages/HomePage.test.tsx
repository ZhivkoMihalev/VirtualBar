import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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

/** `count` reviews, newest first, named `Bottle 01`, `Bottle 02`, … so the order on the page can be read back. */
function reviews(count: number, first = 1): FeedItem[] {
  return Array.from({ length: count }, (_, i) =>
    reviewedItem({ bottleName: `Bottle ${String(first + i).padStart(2, '0')}` }),
  )
}

/** The home page of a logged-in collector, with an API that pages `feed` by `skip` and `take` as the server does. */
function renderPagedFeed(feed: FeedItem[]) {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  vi.mocked(getFeed).mockImplementation(async (skip, take) => feed.slice(skip, skip + take))
  return renderWithProviders(<HomePage />)
}

/** The bottles of the review cards on the page, in the order shown. */
const shownBottles = () => screen.queryAllByRole('link', { name: /^Bottle \d+$/ }).map(link => link.textContent)

const showMore = () => screen.queryByRole('button', { name: bg.home.loadMore })

describe('HomePage — "Покажи още" pages the feed (#127)', () => {
  it('asks for one item more than a page, and appends each next page in order until there is no more', async () => {
    renderPagedFeed(reviews(45))

    await vi.waitFor(() => expect(shownBottles()).toHaveLength(20))
    expect(getFeed).toHaveBeenLastCalledWith(0, 21, 'bg')
    expect(shownBottles()).toEqual(reviews(20).map(item => item.bottleName))

    await userEvent.click(showMore()!)
    await vi.waitFor(() => expect(shownBottles()).toHaveLength(40))
    expect(getFeed).toHaveBeenLastCalledWith(20, 21, 'bg')
    expect(shownBottles()[20]).toBe('Bottle 21')

    await userEvent.click(showMore()!)
    await vi.waitFor(() => expect(shownBottles()).toHaveLength(45))
    expect(getFeed).toHaveBeenLastCalledWith(40, 21, 'bg')
    expect(shownBottles().at(-1)).toBe('Bottle 45')
    expect(showMore()).not.toBeInTheDocument()
  })

  it('shows no button when the feed has 20 items or fewer', async () => {
    renderPagedFeed(reviews(20))

    await vi.waitFor(() => expect(shownBottles()).toHaveLength(20))
    expect(showMore()).not.toBeInTheDocument()
  })

  it('shows the button with exactly 21 items, and the 21st only on the next page', async () => {
    renderPagedFeed(reviews(21))

    await vi.waitFor(() => expect(shownBottles()).toHaveLength(20))
    expect(screen.queryByRole('link', { name: 'Bottle 21' })).not.toBeInTheDocument()

    await userEvent.click(showMore()!)
    expect(await screen.findByRole('link', { name: 'Bottle 21' })).toBeInTheDocument()
    expect(showMore()).not.toBeInTheDocument()
  })

  it('shows an item once when a new one arrives between two pages and pushes it onto the next page', async () => {
    const feed = reviews(30)
    renderPagedFeed(feed)
    await vi.waitFor(() => expect(shownBottles()).toHaveLength(20))

    // A followed collector rates another bottle: everything moves down one place, so the next page starts with the
    // 20th item, already on the page.
    feed.unshift(reviewedItem({ bottleName: 'Bottle 00' }))
    await userEvent.click(showMore()!)

    await vi.waitFor(() => expect(shownBottles()).toHaveLength(30))
    expect(screen.getAllByRole('link', { name: 'Bottle 20' })).toHaveLength(1)
    expect(shownBottles().slice(18, 22)).toEqual(['Bottle 19', 'Bottle 20', 'Bottle 21', 'Bottle 22'])
  })
})
