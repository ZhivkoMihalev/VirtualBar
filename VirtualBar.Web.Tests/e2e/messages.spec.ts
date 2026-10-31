import {
  api,
  test,
  expect,
  FLOW_TIMEOUT,
  bg,
  card,
  chatMessage,
  chatToggle,
  notification,
  openNotifications,
  registerCollector,
  uniqueName,
} from './support'

/**
 * Direct messages through the floating chat widget: starting a conversation from a public bar or from a wish in the
 * marketplace, the unread count, reading a thread (which marks it read) and replying.
 */
test.describe('Messages', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('a collector writes from a public bar; the recipient reads it in the inbox and replies', async ({
    openAs,
  }) => {
    const [sender, recipient] = await Promise.all([registerCollector('sender'), registerCollector('recipient')])
    // Short enough for the inbox preview, which cuts at 40 characters.
    const hello = `Hello from ${sender.displayName}`
    const reply = `Reply to ${sender.displayName}`

    const senderPage = await openAs(sender)
    await senderPage.goto(`/bar/${recipient.id}`)
    await senderPage.getByRole('button', { name: bg.messages.sendMessage, exact: true }).click()
    const senderComposer = senderPage.getByPlaceholder(bg.messages.inputPlaceholder)
    await senderComposer.fill(hello)
    await senderComposer.press('Enter')
    await expect(chatMessage(senderPage, hello)).toBeVisible()
    await expect(senderComposer).toHaveValue('')

    const recipientPage = await openAs(recipient)
    await recipientPage.goto('/')
    const toggle = chatToggle(recipientPage)
    await expect(toggle.locator('[data-slot="badge"]')).toHaveText('1')

    await toggle.click()
    const conversation = recipientPage.getByRole('button', { name: new RegExp(sender.displayName) })
    await expect(conversation).toContainText(hello)
    await expect(conversation.locator('[data-slot="badge"]')).toHaveText('1')

    await conversation.click()
    await expect(chatMessage(recipientPage, hello)).toBeVisible()
    // Viewing the thread marks it read.
    await expect(conversation.locator('[data-slot="badge"]')).toHaveCount(0)
    await expect(toggle.locator('[data-slot="badge"]')).toHaveCount(0)

    await recipientPage.getByPlaceholder(bg.messages.inputPlaceholder).fill(reply)
    await recipientPage.getByRole('button', { name: bg.messages.send, exact: true }).click()
    await expect(chatMessage(recipientPage, reply)).toBeVisible()
    await expect(conversation).toContainText(`${bg.messages.you}: ${reply}`)

    await senderPage.reload()
    await expect(chatToggle(senderPage).locator('[data-slot="badge"]')).toHaveText('1')
    await chatToggle(senderPage).click()
    await senderPage.getByRole('button', { name: new RegExp(recipient.displayName) }).click()
    await expect(chatMessage(senderPage, reply)).toBeVisible()
    await expect(chatMessage(senderPage, hello)).toBeVisible()
  })

  test('a seller answers a wish from the marketplace, and the notification opens the conversation', async ({
    openAs,
  }) => {
    const [wisher, seller] = await Promise.all([registerCollector('wisher'), registerCollector('seller')])
    const wishName = uniqueName('Wish')
    const wish = await api<{ id: string }>(wisher, 'POST', '/wishlist', { bottleName: wishName, category: 'Cognac' })

    try {
      const sellerPage = await openAs(seller)
      await sellerPage.goto('/marketplace')
      await sellerPage.getByRole('tab', { name: bg.marketplace.tabSearch }).click()
      await card(sellerPage, wishName).getByRole('button', { name: bg.wishList.contactBtn }).click()

      const contact = sellerPage.getByRole('dialog', { name: bg.wishList.contactTitle })
      await expect(contact.getByRole('textbox')).toHaveValue(bg.wishList.contactDefaultMessage)
      await contact.getByRole('button', { name: bg.wishList.contactSend, exact: true }).click()
      await expect(contact).toHaveCount(0)
      // Sending opens the conversation in the chat widget.
      await expect(chatMessage(sellerPage, bg.wishList.contactDefaultMessage)).toBeVisible()

      const wisherPage = await openAs(wisher)
      await wisherPage.goto('/')
      await openNotifications(wisherPage)
      await notification(wisherPage, seller.displayName, bg.notifications.newMessage).click()
      await expect(chatMessage(wisherPage, bg.wishList.contactDefaultMessage)).toBeVisible()
      await expect(chatToggle(wisherPage).locator('[data-slot="badge"]')).toHaveCount(0)
    } finally {
      // A category-only wish matches every Cognac listed later: leave none behind.
      await api(wisher, 'DELETE', `/wishlist/${wish.id}`)
    }
  })
})
