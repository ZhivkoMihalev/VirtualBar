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
 * The marketplace: listing a bottle for sale from one's own bar (also part of a stack), finding the listing as a
 * visitor, unlisting it, and the "Търсене" tab where a collector publishes a wish and is notified when a matching
 * bottle is listed.
 */
test.describe('Marketplace', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('a seller lists part of a stack; visitors find it in the marketplace until it is unlisted', async ({
    openAs,
  }) => {
    const seller = await registerCollector('seller')
    const name = uniqueName('Stack')
    await createBottle(seller, { name, quantity: 3 })

    const sellerPage = await openAs(seller)
    await sellerPage.goto('/dashboard')
    const panel = await openBottlePanel(sellerPage, name)

    // The owner sells, never makes an offer on their own bottle.
    await expect(panel.getByRole('button', { name: bg.offers.makeOffer })).toHaveCount(0)

    await panel.getByPlaceholder(bg.bottle.askingPrice).fill('150')
    await panel.getByRole('combobox').filter({ hasText: 'USD' }).click()
    await sellerPage.getByRole('option', { name: 'EUR', exact: true }).click()
    // A stack starts at one bottle for sale; offer two of the three.
    await panel.getByRole('button', { name: bg.bottle.forSaleIncrease }).click()
    await panel.getByRole('button', { name: bg.bottle.listForSale }).click()

    await expect(panel.getByText(bg.bottle.forSale, { exact: true })).toBeVisible()
    await expect(panel).toContainText('EUR 150')
    await expect(panel).toContainText(bg.bottle.perBottle)
    await expect(panel).toContainText(fill(bg.bottle.forSaleOfOwned, { forSale: 2, owned: 3 }))

    const visitor = await openAs(null)
    await visitor.goto('/marketplace')
    await searchMarketplace(visitor, name)
    const listing = card(visitor, name)
    await expect(listing).toContainText('×2')
    await expect(listing).toContainText('EUR 150')
    await expect(listing).toContainText(fill(bg.marketplace.by, { name: seller.displayName }))

    await listing.getByRole('button', { name: bg.marketplace.viewBottle }).click()
    const visitorPanel = visitor.getByRole('dialog', { name })
    await expect(visitorPanel).toContainText(bg.bottle.askingPrice)
    await expect(visitorPanel).toContainText(fill(bg.bottle.availableForSale_other, { count: 2 }))
    // Offers need an account.
    await expect(visitorPanel.getByRole('button', { name: bg.offers.makeOffer })).toHaveCount(0)

    await panel.getByRole('button', { name: bg.bottle.removeFromSale }).click()
    await expect(panel.getByRole('button', { name: bg.bottle.listForSale })).toBeVisible()
    await expect(panel.getByText(bg.bottle.forSale, { exact: true })).toHaveCount(0)

    await visitor.goto('/marketplace')
    await searchMarketplace(visitor, name)
    await expect(visitor.getByText(bg.marketplace.empty)).toBeVisible()
    await expect(card(visitor, name)).toHaveCount(0)
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
