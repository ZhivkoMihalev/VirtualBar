import { describe, expect, it } from 'vitest'
import { toBarcode } from '@/lib/barcode'

describe('toBarcode', () => {
  it.each(['50103140', '082184090466', '5000267014203', '15000267014200'])(
    'keeps %s, an EAN-8, UPC-A, EAN-13 or ITF-14 the API accepts (#49)',
    barcode => {
      expect(toBarcode(barcode)).toBe(barcode)
    },
  )

  it('trims the value', () => {
    expect(toBarcode('  5000267014203  ')).toBe('5000267014203')
  })

  it.each(['', '   ', '1234567', '123456789012345', '50002670142AB', '5000267-014203'])(
    'drops "%s", which the API would refuse, so it cannot block adding the bottle',
    barcode => {
      expect(toBarcode(barcode)).toBeUndefined()
    },
  )
})
