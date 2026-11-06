/** The earliest year a bottle's vintage or a product's year may take, as on the server (#59). */
export const EARLIEST_YEAR = 1800

/** The latest: next year, since an edition can be announced before its year starts. */
export const latestYear = () => new Date().getUTCFullYear() + 1

/** Whether the text is a year the form accepts: four digits between {@link EARLIEST_YEAR} and {@link latestYear}. */
export function isYear(text: string): boolean {
  if (!/^\d{4}$/.test(text)) return false
  const year = Number(text)
  return year >= EARLIEST_YEAR && year <= latestYear()
}

/**
 * The product's name with its year swapped for another: the name of another vintage of the same line, which is
 * how the catalog names them ("Millésime 1988" → "Millésime 1983"). Only the year as a whole number is replaced.
 */
export function withYear(name: string, from: number, to: number): string {
  return name.replace(new RegExp(`(?<!\\d)${from}(?!\\d)`), String(to))
}
