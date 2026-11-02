/** The only currencies the app accepts (#98). The API refuses any other for a listing or an offer. */
export const CURRENCIES: readonly string[] = ['EUR', 'GBP', 'USD']

export const DEFAULT_CURRENCY = 'EUR'

/**
 * The currency a sale or offer form starts with: the bottle's own when the app still accepts it, otherwise the default.
 * A bottle listed before #98 may carry a currency the API would now refuse.
 */
export function formCurrency(currency: string | null | undefined): string {
  return currency && CURRENCIES.includes(currency) ? currency : DEFAULT_CURRENCY
}
