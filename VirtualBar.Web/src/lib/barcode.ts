/** A GS1 barcode as the API accepts it (#49): EAN-8, UPC-A, EAN-13 or ITF-14, so 8 to 14 digits. */
export const BARCODE_PATTERN = /^\d{8,14}$/

/**
 * The barcode to save with a bottle: the trimmed value when it is 8 to 14 digits, otherwise undefined. The API refuses
 * any other value, and a mistyped barcode must never block adding the bottle.
 */
export function toBarcode(value: string): string | undefined {
  const trimmed = value.trim()

  return BARCODE_PATTERN.test(trimmed) ? trimmed : undefined
}
