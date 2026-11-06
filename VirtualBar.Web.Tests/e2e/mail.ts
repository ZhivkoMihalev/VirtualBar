import { readdir, readFile } from 'node:fs/promises'
import path from 'node:path'
import { expect } from '@playwright/test'

/**
 * The emails the API writes in E2E runs. With `Email:PickupDirectory` set, the API writes every email as an .eml file
 * instead of sending it; `E2E_MAIL_DIR` points the tests at the same directory (`.claude/rules/ci/e2e.md`), so they open
 * the very link the API put in the email.
 */
const MAIL_DIR = process.env.E2E_MAIL_DIR

function decodeQuotedPrintable(text: string): string {
  const joined = text.replace(/=\r?\n/g, '')
  const bytes: number[] = []
  for (let i = 0; i < joined.length; i++) {
    const hex = joined.slice(i + 1, i + 3)
    if (joined[i] === '=' && /^[0-9A-F]{2}$/i.test(hex)) {
      bytes.push(parseInt(hex, 16))
      i += 2
    } else {
      bytes.push(...Buffer.from(joined[i], 'utf8'))
    }
  }
  return Buffer.from(bytes).toString('utf8')
}

/** Headers (folded lines joined) and decoded body of a single-part email, whichever transfer encoding MIME chose. */
function parseEmail(raw: string): { headers: string; body: string } {
  const split = raw.search(/\r?\n\r?\n/)
  const headers = raw.slice(0, split).replace(/\r?\n[ \t]+/g, ' ')
  const body = raw.slice(split).replace(/^\r?\n\r?\n/, '')
  const encoding = /^content-transfer-encoding:\s*(\S+)/im.exec(headers)?.[1]?.toLowerCase()
  if (encoding === 'base64') return { headers, body: Buffer.from(body.replace(/\s+/g, ''), 'base64').toString('utf8') }
  if (encoding === 'quoted-printable') return { headers, body: decodeQuotedPrintable(body) }
  return { headers, body }
}

/** The app path of the newest confirmation link emailed to this address, or null while there is none. */
export async function findConfirmationLink(email: string): Promise<string | null> {
  if (!MAIL_DIR) {
    throw new Error("Set E2E_MAIL_DIR to the directory of the API's Email:PickupDirectory (.claude/rules/ci/e2e.md).")
  }
  let files: string[]
  try {
    files = await readdir(MAIL_DIR)
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null // no email written yet
    throw error
  }
  // The file names start with a UTC timestamp, so the newest email sorts last.
  for (const file of files.filter(name => name.endsWith('.eml')).sort().reverse()) {
    const { headers, body } = parseEmail(await readFile(path.join(MAIL_DIR, file), 'utf8'))
    const to = /^to:(.*)$/im.exec(headers)?.[1] ?? ''
    if (!to.toLowerCase().includes(email.toLowerCase())) continue
    const href = /href="([^"]*\/confirm-email\?[^"]*)"/.exec(body)?.[1]
    if (!href) continue
    const url = new URL(href.replaceAll('&amp;', '&'))
    return url.pathname + url.search
  }
  return null
}

/**
 * Waits for the API to write a confirmation email to this address and returns the app path its link opens. With
 * `other`, waits for a link different from that one: a newer email than the one the test already has.
 */
export async function waitForConfirmationLink(email: string, other?: string): Promise<string> {
  let link: string | null = null
  await expect
    .poll(
      async () => {
        link = await findConfirmationLink(email)
        return link !== null && link !== other
      },
      { message: `a${other ? ' new' : ''} confirmation email to ${email} in ${MAIL_DIR}`, timeout: 10_000 },
    )
    .toBe(true)
  return link!
}
