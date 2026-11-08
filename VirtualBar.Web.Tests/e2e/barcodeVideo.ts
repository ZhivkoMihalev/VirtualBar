import { writeFileSync } from 'node:fs'

/**
 * A fake camera for the barcode scanner (#49): Chromium plays a `.y4m` video file instead of a camera when launched
 * with `--use-file-for-fake-video-capture`, and this draws a real EAN-13 into one, so the app's own scanner reads it.
 */

// The bar patterns of EAN-13: L (odd parity) and G (even parity) on the left half, R on the right half.
const L = ['0001101', '0011001', '0010011', '0111101', '0100011', '0110001', '0101111', '0111011', '0110111', '0001011']
const G = ['0100111', '0110011', '0011011', '0100001', '0011101', '0111001', '0000101', '0010001', '0001001', '0010111']
const R = ['1110010', '1100110', '1101100', '1000010', '1011100', '1001110', '1010000', '1000100', '1001000', '1110100']
// The first digit is not drawn: it chooses which of the next six digits use L or G.
const PARITY = ['LLLLLL', 'LLGLGG', 'LLGGLG', 'LLGGGL', 'LGLLGG', 'LGGLLG', 'LGGGLL', 'LGLGLG', 'LGLGGL', 'LGGLGL']

/** The EAN-13 of 12 digits: the digits and their check digit, which every scanner verifies. */
export function ean13(body: string): string {
  if (!/^\d{12}$/.test(body)) throw new Error(`An EAN-13 body is 12 digits, not "${body}".`)
  const sum = [...body].reduce((total, digit, i) => total + Number(digit) * (i % 2 === 0 ? 1 : 3), 0)
  return body + String((10 - (sum % 10)) % 10)
}

/**
 * A barcode no real product has, so runs against the same database never collide: GS1 keeps prefixes 200–299 for
 * numbers a shop gives its own goods.
 */
export function testBarcode(): string {
  let body = '2'
  for (let i = 0; i < 11; i++) body += String(Math.floor(Math.random() * 10))
  return ean13(body)
}

/** The 95 modules of an EAN-13, '1' for a bar: start, six left digits, middle, six right digits, end. */
function modules(barcode: string): string {
  const digits = [...barcode].map(Number)
  const parity = PARITY[digits[0]]
  const left = digits.slice(1, 7).map((d, i) => (parity[i] === 'L' ? L[d] : G[d])).join('')
  const right = digits.slice(7).map(d => R[d]).join('')
  return `101${left}01010${right}101`
}

const WIDTH = 640
const HEIGHT = 480
const MODULE_PX = 4
const BAR_HEIGHT = 280
const WHITE = 235
const BLACK = 16

/**
 * Writes one video frame of `barcode`, black bars on white, to `path` as YUV4MPEG2 (4:2:0). Chromium loops it, so the
 * scanner sees the barcode for as long as it looks.
 */
export function writeBarcodeVideo(barcode: string, path: string) {
  const bars = modules(barcode)
  const luma = Buffer.alloc(WIDTH * HEIGHT, WHITE)
  const left = Math.floor((WIDTH - bars.length * MODULE_PX) / 2)
  const top = Math.floor((HEIGHT - BAR_HEIGHT) / 2)
  for (let y = top; y < top + BAR_HEIGHT; y++) {
    for (let m = 0; m < bars.length; m++) {
      if (bars[m] === '1') luma.fill(BLACK, y * WIDTH + left + m * MODULE_PX, y * WIDTH + left + (m + 1) * MODULE_PX)
    }
  }
  // Grey has no colour: both chroma planes sit at their middle value.
  const chroma = Buffer.alloc((WIDTH / 2) * (HEIGHT / 2) * 2, 128)
  const header = Buffer.from(`YUV4MPEG2 W${WIDTH} H${HEIGHT} F10:1 Ip A1:1 C420jpeg\nFRAME\n`)
  writeFileSync(path, Buffer.concat([header, luma, chroma]))
}
