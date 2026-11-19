import {
  api,
  test,
  expect,
  FLOW_TIMEOUT,
  bg,
  card,
  createBottle,
  fill,
  notification,
  openBottlePanel,
  openNotifications,
  registerCollector,
  uniqueName,
} from './support'

/**
 * Offers: any collector can offer on any bottle of another collector, listed for sale or not. The seller accepts or
 * declines from /offers, the buyer can withdraw a pending offer, and each answer reaches the other side as a
 * notification. An accepted offer reserves the bottle until the buyer confirms receiving it, which moves the bottle to
 * the buyer's bar (#130).
 */
test.describe('Offers', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('a buyer offers on a bottle that is not for sale; the seller accepts and both sides see it', async ({
    openAs,
  }) => {
    const [seller, buyer] = await Promise.all([registerCollector('seller'), registerCollector('buyer')])
    const name = uniqueName('Offer')
    const note = `Would you part with it, ${seller.displayName}?`
    await createBottle(seller, { name })

    const visitor = await openAs(null)
    await visitor.goto(`/bar/${seller.id}`)
    const visitorPanel = await openBottlePanel(visitor, name)
    await expect(visitorPanel.getByRole('button', { name: bg.offers.makeOffer })).toHaveCount(0)

    const buyerPage = await openAs(buyer)
    await buyerPage.goto(`/bar/${seller.id}`)
    const panel = await openBottlePanel(buyerPage, name)
    await panel.getByRole('button', { name: bg.offers.makeOffer }).click()

    const form = buyerPage.getByRole('dialog', { name: bg.offers.offerModalTitle })
    // Not listed, so there is no asking price to start from: an empty price is refused before any request.
    await form.getByRole('button', { name: bg.offers.submit }).click()
    await expect(form.getByText(bg.offers.priceInvalid)).toBeVisible()

    await form.getByLabel(bg.offers.price, { exact: true }).fill('120')
    await form.getByLabel(bg.offers.message).fill(note)
    await form.getByRole('button', { name: bg.offers.submit }).click()
    await expect(form).toHaveCount(0)

    await buyerPage.goto('/offers')
    await buyerPage.getByRole('tab', { name: bg.offers.tabSent }).click()
    const sent = card(buyerPage, name)
    await expect(sent).toContainText(bg.offers.statusPending)
    await expect(sent).toContainText(fill(bg.offers.by, { name: seller.displayName }))
    await expect(sent).toContainText('EUR 120')
    await expect(sent).toContainText(note)
    await expect(sent.getByRole('button', { name: bg.offers.withdraw })).toBeVisible()

    const sellerPage = await openAs(seller)
    await sellerPage.goto('/')
    await openNotifications(sellerPage)
    await notification(sellerPage, buyer.displayName, fill(bg.notifications.offerReceived, { bottle: name })).click()
    await expect(sellerPage).toHaveURL(/\/offers$/)

    const received = card(sellerPage, name)
    await expect(received).toContainText(fill(bg.offers.by, { name: buyer.displayName }))
    await expect(received).toContainText('EUR 120')
    await received.getByRole('button', { name: bg.offers.accept }).click()
    // Accepted, the bottle is reserved until the buyer confirms receiving it (#130): the seller can only cancel.
    await expect(received).toContainText(bg.offers.statusAwaitingReceipt)
    await expect(received.getByRole('button', { name: bg.offers.accept })).toHaveCount(0)
    await expect(received.getByRole('button', { name: bg.offers.cancelDeal })).toBeVisible()

    // The buyer is asked on the next page load whether the bottle arrived; "not yet" puts it off.
    await buyerPage.reload()
    const reminder = buyerPage.getByRole('dialog', { name: bg.offers.reminderTitle })
    await expect(reminder).toContainText(name)
    await reminder.getByRole('button', { name: bg.offers.reminderLater }).click()
    await expect(reminder).toHaveCount(0)

    await openNotifications(buyerPage)
    await expect(
      notification(buyerPage, seller.displayName, fill(bg.notifications.offerAccepted, { bottle: name })),
    ).toBeVisible()
    await buyerPage.keyboard.press('Escape')
    await buyerPage.getByRole('tab', { name: bg.offers.tabSent }).click()
    await expect(sent).toContainText(bg.offers.statusAwaitingReceipt)
    await expect(sent.getByRole('button', { name: bg.offers.withdraw })).toHaveCount(0)
    await sent.getByRole('button', { name: bg.offers.confirmReceipt }).click()
    await expect(sent).toContainText(bg.offers.statusAccepted)

    // The bottle moved: it is in the buyer's bar, bought from the seller, and gone from the seller's.
    await buyerPage.goto(`/bar/${buyer.id}`)
    const bought = await openBottlePanel(buyerPage, name)
    await expect(bought).toContainText(bg.bottle.acquiredFrom)
    await expect(bought.getByRole('link', { name: seller.displayName })).toBeVisible()
    await sellerPage.goto(`/bar/${seller.id}`)
    await expect(sellerPage.getByText(bg.publicBar.empty)).toBeVisible()
  })

  test('the seller declines one offer and the buyer withdraws another', async ({ openAs }) => {
    const [seller, buyer] = await Promise.all([registerCollector('seller'), registerCollector('buyer')])
    const declinedName = uniqueName('Declined')
    const withdrawnName = uniqueName('Withdrawn')
    const [declinedBottle, withdrawnBottle] = await Promise.all([
      createBottle(seller, { name: declinedName }),
      createBottle(seller, { name: withdrawnName }),
    ])
    await api(buyer, 'POST', '/offers', { bottleId: declinedBottle.id, offeredPrice: 80, currency: 'EUR' })
    await api(buyer, 'POST', '/offers', { bottleId: withdrawnBottle.id, offeredPrice: 95, currency: 'EUR' })

    const sellerPage = await openAs(seller)
    await sellerPage.goto('/offers')
    const toDecline = card(sellerPage, declinedName)
    await toDecline.getByRole('button', { name: bg.offers.decline }).click()
    await expect(toDecline).toContainText(bg.offers.statusDeclined)
    await expect(toDecline.getByRole('button')).toHaveCount(0)

    const buyerPage = await openAs(buyer)
    await buyerPage.goto('/offers')
    await buyerPage.getByRole('tab', { name: bg.offers.tabSent }).click()
    await expect(card(buyerPage, declinedName)).toContainText(bg.offers.statusDeclined)
    await expect(card(buyerPage, declinedName).getByRole('button')).toHaveCount(0)

    const toWithdraw = card(buyerPage, withdrawnName)
    await toWithdraw.getByRole('button', { name: bg.offers.withdraw }).click()
    await expect(toWithdraw).toContainText(bg.offers.statusWithdrawn)
    await expect(toWithdraw.getByRole('button')).toHaveCount(0)

    await openNotifications(buyerPage)
    await expect(
      notification(buyerPage, seller.displayName, fill(bg.notifications.offerDeclined, { bottle: declinedName })),
    ).toBeVisible()

    // A withdrawn offer stays on the seller's list, with nothing left to answer.
    await sellerPage.reload()
    await expect(card(sellerPage, withdrawnName)).toContainText(bg.offers.statusWithdrawn)
    await expect(card(sellerPage, withdrawnName).getByRole('button')).toHaveCount(0)
  })
})
