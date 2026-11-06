import {
  api,
  test,
  expect,
  FLOW_TIMEOUT,
  bg,
  card,
  createBottle,
  fill,
  firstDistillery,
  notification,
  openBottlePanel,
  openNotifications,
  registerCollector,
  searchMarketplace,
  uniqueName,
} from './support'

/**
 * The marketplace, for logged-in collectors only: listing a bottle for sale from one's own bar (also part of a stack),
 * finding the listing as another collector, unlisting it, the "Търсене" tab where a collector publishes a wish and is
 * notified when a matching bottle is listed, the login gate a visitor sees instead (#100), and the asking price on a
 * public bar, which a visitor does not see (#103).
 */
test.describe('Marketplace', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('a visitor sees the login gate and stays on the marketplace, even on the "Търсене" tab (#100)', async ({
    openAs,
  }) => {
    const visitor = await openAs(null)
    await visitor.goto('/marketplace')

    await expect(visitor.getByRole('heading', { name: bg.marketplace.loginGateTitle })).toBeVisible()
    await expect(visitor.getByRole('tab', { name: bg.marketplace.tabSearch })).toHaveCount(0)
    // Inside the page: the NavBar has its own "ВХОД" link, which matches the name too.
    const gate = visitor.getByRole('main')
    await expect(gate.getByRole('link', { name: bg.marketplace.loginGateLogin })).toHaveAttribute('href', '/login')
    await expect(gate.getByRole('link', { name: bg.marketplace.loginGateRegister })).toHaveAttribute('href', '/register')
    await expect(visitor).toHaveURL(/\/marketplace$/)
  })

  test('on a public bar a visitor sees that a bottle is for sale but not its price; a collector sees it (#103)', async ({
    openAs,
  }) => {
    const [seller, buyer] = await Promise.all([registerCollector('seller'), registerCollector('buyer')])
    const name = uniqueName('Priced')
    const listed = await createBottle(seller, { name, quantity: 3 })
    await api(seller, 'POST', `/bottles/${listed.id}/list-for-sale`, { askingPrice: 175, currency: 'GBP', quantity: 2 })

    const visitor = await openAs(null)
    await visitor.goto(`/bar/${seller.id}`)
    const visitorPanel = await openBottlePanel(visitor, name)
    await expect(visitorPanel.getByText(bg.bottle.forSale, { exact: true })).toBeVisible()
    await expect(visitorPanel).toContainText(fill(bg.bottle.availableForSale_other, { count: 2 }))
    await expect(visitorPanel.getByRole('link', { name: bg.bottle.loginToSeePrice })).toHaveAttribute('href', '/login')
    await expect(visitorPanel).not.toContainText('GBP 175')

    const buyerPage = await openAs(buyer)
    await buyerPage.goto(`/bar/${seller.id}`)
    const buyerPanel = await openBottlePanel(buyerPage, name)
    await expect(buyerPanel).toContainText('GBP 175')
    await expect(buyerPanel).toContainText(bg.bottle.perBottle)
    await expect(buyerPanel.getByRole('link', { name: bg.bottle.loginToSeePrice })).toHaveCount(0)
  })

  test('a seller lists part of a stack; other collectors find it in the marketplace until it is unlisted', async ({
    openAs,
  }) => {
    const [seller, buyer] = await Promise.all([registerCollector('seller'), registerCollector('buyer')])
    const name = uniqueName('Stack')
    await createBottle(seller, { name, quantity: 3 })

    const sellerPage = await openAs(seller)
    await sellerPage.goto('/dashboard')
    const panel = await openBottlePanel(sellerPage, name)

    // The owner sells, never makes an offer on their own bottle.
    await expect(panel.getByRole('button', { name: bg.offers.makeOffer })).toHaveCount(0)

    await panel.getByPlaceholder(bg.bottle.askingPrice).fill('150')
    // Only EUR, GBP and USD, EUR first and preselected (#98).
    await panel.getByRole('combobox').filter({ hasText: 'EUR' }).click()
    await expect(sellerPage.getByRole('option')).toHaveText(['EUR', 'GBP', 'USD'])
    await sellerPage.getByRole('option', { name: 'GBP', exact: true }).click()
    // A stack starts at one bottle for sale; offer two of the three.
    await panel.getByRole('button', { name: bg.bottle.forSaleIncrease }).click()
    await panel.getByRole('button', { name: bg.bottle.listForSale }).click()

    await expect(panel.getByText(bg.bottle.forSale, { exact: true })).toBeVisible()
    await expect(panel).toContainText('GBP 150')
    await expect(panel).toContainText(bg.bottle.perBottle)
    await expect(panel).toContainText(fill(bg.bottle.forSaleOfOwned, { forSale: 2, owned: 3 }))

    const buyerPage = await openAs(buyer)
    await buyerPage.goto('/marketplace')
    await searchMarketplace(buyerPage, name)
    const listing = card(buyerPage, name)
    await expect(listing).toContainText('×2')
    await expect(listing).toContainText('GBP 150')
    await expect(listing).toContainText(fill(bg.marketplace.by, { name: seller.displayName }))

    await listing.getByRole('button', { name: bg.marketplace.viewBottle }).click()
    const buyerPanel = buyerPage.getByRole('dialog', { name })
    await expect(buyerPanel).toContainText(bg.bottle.askingPrice)
    await expect(buyerPanel).toContainText(fill(bg.bottle.availableForSale_other, { count: 2 }))
    // Another collector may make an offer.
    await expect(buyerPanel.getByRole('button', { name: bg.offers.makeOffer })).toBeVisible()

    await panel.getByRole('button', { name: bg.bottle.removeFromSale }).click()
    await expect(panel.getByRole('button', { name: bg.bottle.listForSale })).toBeVisible()
    await expect(panel.getByText(bg.bottle.forSale, { exact: true })).toHaveCount(0)

    await buyerPage.goto('/marketplace')
    await searchMarketplace(buyerPage, name)
    await expect(buyerPage.getByText(bg.marketplace.empty)).toBeVisible()
    await expect(card(buyerPage, name)).toHaveCount(0)
  })

  test('a wish published under "Търсене" is matched when a bottle from that distillery is listed', async ({
    openAs,
  }) => {
    const [wisher, seller] = await Promise.all([registerCollector('wisher'), registerCollector('seller')])
    const distillery = await firstDistillery('Rum')
    const wish = uniqueName('Wish')

    const wisherPage = await openAs(wisher)
    await wisherPage.goto('/marketplace')
    await wisherPage.getByRole('tab', { name: bg.marketplace.tabSearch }).click()
    await wisherPage.getByRole('button', { name: bg.wishList.publishSearch }).click()

    const form = wisherPage.getByRole('dialog', { name: bg.wishList.publishSearchTitle })
    await form.getByLabel(bg.wishList.bottleName).fill(wish)
    // Category first: choosing a category clears the distillery.
    await form.getByRole('combobox', { name: bg.wishList.category }).click()
    await wisherPage.getByRole('option', { name: 'Rum', exact: true }).click()
    await form.getByRole('combobox').filter({ hasText: bg.wishList.distilleryPlaceholder }).click()
    await wisherPage.getByPlaceholder(bg.distillerySelect.placeholder).fill(distillery.name)
    await wisherPage
      .getByRole('option')
      .filter({ has: wisherPage.getByText(distillery.name, { exact: true }) })
      .first()
      .click()
    await form.getByRole('button', { name: bg.wishList.addBtn, exact: true }).click()
    await expect(form).toHaveCount(0)

    const wishCard = card(wisherPage, wish)
    await expect(wishCard).toContainText(wisher.displayName)
    await expect(wishCard).toContainText(distillery.name)
    await expect(wishCard).toContainText('Rum')
    // The owner can remove their wish; nobody messages themselves.
    await expect(wishCard.getByRole('button', { name: bg.wishList.remove })).toBeVisible()
    await expect(wishCard.getByRole('button', { name: bg.wishList.contactBtn })).toHaveCount(0)

    const bottleName = uniqueName('Match')
    const bottle = await createBottle(seller, { name: bottleName, category: 'Rum', distilleryId: distillery.id })
    await api(seller, 'POST', `/bottles/${bottle.id}/list-for-sale`, { askingPrice: 90, currency: 'EUR', quantity: 1 })

    await wisherPage.goto('/')
    await openNotifications(wisherPage)
    await notification(
      wisherPage,
      seller.displayName,
      fill(bg.notifications.wishListMatch, { bottle: bottleName }),
    ).click()
    await expect(wisherPage).toHaveURL(/\/marketplace$/)

    await wisherPage.getByRole('tab', { name: bg.marketplace.tabSearch }).click()
    await wishCard.getByRole('button', { name: bg.wishList.remove }).click()
    await wisherPage.getByRole('alertdialog').getByRole('button', { name: bg.wishList.removeConfirm }).click()
    await expect(wishCard).toHaveCount(0)
  })
})
