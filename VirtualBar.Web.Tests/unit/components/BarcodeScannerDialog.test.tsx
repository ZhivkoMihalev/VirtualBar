import { useState } from 'react'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import BarcodeScannerDialog from '@/components/BarcodeScannerDialog'
import bg from '@/i18n/bg.json'
import { camera, cameraFails, cameraReads, cameraSeesNothing, setCameraSupported } from '../fakeCamera'
import { renderWithProviders } from '../testUtils'

vi.mock('@zxing/browser', async () => (await import('../fakeCamera')).zxingBrowserMock())

const BARCODE = '5000267014203'

/** The dialog as a caller uses it: opened by a button, closed by itself or by a read barcode. */
function Harness({ onDetected }: { onDetected: (barcode: string) => void }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        open scanner
      </button>
      <BarcodeScannerDialog
        open={open}
        onOpenChange={setOpen}
        onDetected={barcode => {
          setOpen(false)
          onDetected(barcode)
        }}
      />
    </>
  )
}

async function openScanner(onDetected = vi.fn()) {
  renderWithProviders(<Harness onDetected={onDetected} />)
  await userEvent.click(screen.getByRole('button', { name: 'open scanner' }))
  return onDetected
}

describe('BarcodeScannerDialog', () => {
  beforeEach(() => setCameraSupported(true))

  it('hands over the barcode the camera reads and turns the camera off (#49)', async () => {
    cameraReads(BARCODE)

    const onDetected = await openScanner()

    await vi.waitFor(() => expect(onDetected).toHaveBeenCalledWith(BARCODE))
    expect(camera.stop).toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('asks the browser for the rear camera, with no sound', async () => {
    cameraSeesNothing()

    await openScanner()

    await vi.waitFor(() => expect(camera.decode).toHaveBeenCalled())
    expect(camera.decode.mock.calls[0][0]).toEqual({ video: { facingMode: { ideal: 'environment' } }, audio: false })
  })

  it('turns the camera off when the dialog is closed without a barcode', async () => {
    cameraSeesNothing()
    await openScanner()
    await vi.waitFor(() => expect(camera.decode).toHaveBeenCalled())

    await userEvent.click(screen.getByRole('button', { name: bg.barcodeScanner.close }))

    await vi.waitFor(() => expect(camera.stop).toHaveBeenCalled())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it.each([
    ['NotAllowedError', bg.barcodeScanner.denied],
    ['SecurityError', bg.barcodeScanner.denied],
    ['NotFoundError', bg.barcodeScanner.noCamera],
    ['OverconstrainedError', bg.barcodeScanner.noCamera],
    ['NotReadableError', bg.barcodeScanner.unavailable],
    ['NotSupportedError', bg.barcodeScanner.unavailable],
  ])('says why when the camera fails to start with %s', async (error, message) => {
    cameraFails(error)

    await openScanner()

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })

  it('says the camera needs HTTPS where the browser offers none, without trying to start it', async () => {
    setCameraSupported(false)

    await openScanner()

    expect(await screen.findByRole('alert')).toHaveTextContent(bg.barcodeScanner.insecure)
    expect(camera.decode).not.toHaveBeenCalled()
  })
})
