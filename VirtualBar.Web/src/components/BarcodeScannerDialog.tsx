import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Camera, Loader2 } from 'lucide-react'
import type { IScannerControls } from '@zxing/browser'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

/** Why the camera cannot scan, or that it is starting or scanning. */
type ScannerStatus = 'starting' | 'scanning' | 'denied' | 'noCamera' | 'insecure' | 'unavailable'

// The rear camera on a phone; a laptop has only the one camera and takes it anyway.
const CONSTRAINTS: MediaStreamConstraints = { video: { facingMode: { ideal: 'environment' } }, audio: false }

/**
 * A reader for the barcodes spirits carry (EAN-13, EAN-8, UPC-A, UPC-E), which also checks their check digit. The
 * library is loaded only when a scan starts, so the app's main bundle does not carry it.
 */
async function createReader() {
  const [{ BrowserMultiFormatReader }, { BarcodeFormat, DecodeHintType }] = await Promise.all([
    import('@zxing/browser'),
    import('@zxing/library'),
  ])
  const hints = new Map([
    [DecodeHintType.POSSIBLE_FORMATS, [BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A, BarcodeFormat.UPC_E]],
  ])
  return new BrowserMultiFormatReader(hints, { delayBetweenScanAttempts: 100 })
}

/**
 * The status a failed camera start means: the user refused it, the device has none, or this browser cannot give one
 * (an in-app browser, or a headless one, answers `NotSupportedError`).
 */
function statusOf(error: unknown): ScannerStatus {
  const name = error instanceof Error || error instanceof DOMException ? error.name : ''
  if (name === 'NotAllowedError' || name === 'SecurityError') return 'denied'
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'noCamera'
  return 'unavailable'
}

/** Whether this page may ask for a camera at all: browsers offer one only on HTTPS (or localhost). */
const cameraSupported = () => typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia

/**
 * The live scan, mounted only while the dialog is open, so every opening starts from a fresh state. The camera is
 * stopped on the first barcode read and whenever the dialog closes, so its light never stays on.
 */
function Scanner({ onDetected }: { onDetected: (barcode: string) => void }) {
  const { t } = useTranslation()
  const [video, setVideo] = useState<HTMLVideoElement | null>(null)
  const [status, setStatus] = useState<ScannerStatus>(() => (cameraSupported() ? 'starting' : 'insecure'))
  const onDetectedRef = useRef(onDetected)

  useEffect(() => {
    onDetectedRef.current = onDetected
  }, [onDetected])

  useEffect(() => {
    if (!video || !cameraSupported()) return

    let done = false
    let controls: IScannerControls | undefined

    createReader()
      .then(reader =>
        reader.decodeFromConstraints(CONSTRAINTS, video, (result, _error, scan) => {
          if (!result || done) return
          done = true
          scan.stop()
          onDetectedRef.current(result.getText())
        }),
      )
      .then(started => {
        controls = started
        if (done) started.stop()
        else setStatus('scanning')
      })
      .catch((error: unknown) => {
        if (!done) setStatus(statusOf(error))
      })

    return () => {
      done = true
      controls?.stop()
    }
  }, [video])

  const problem = status !== 'starting' && status !== 'scanning'

  return (
    <div className="space-y-3">
      {problem ? (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t(`barcodeScanner.${status}`)}
        </p>
      ) : (
        <div className="relative overflow-hidden rounded-md bg-black">
          <video
            ref={setVideo}
            aria-label={t('barcodeScanner.video')}
            muted
            playsInline
            className="aspect-[4/3] w-full object-cover"
          />
          {/* Where to hold the barcode: a frame in the middle of the picture. */}
          <div className="pointer-events-none absolute inset-x-[12%] top-1/2 h-1/3 -translate-y-1/2 rounded-md border-2 border-primary/80" />
          {status === 'starting' && (
            <div className="absolute inset-0 flex items-center justify-center gap-2 text-sm text-primary">
              <Loader2 className="size-4 animate-spin" />
              {t('barcodeScanner.starting')}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** The barcode read, 8 to 14 digits. The dialog does not close itself: the caller closes it. */
  onDetected: (barcode: string) => void
}

/**
 * Reads a bottle's barcode with the camera (#49). Where there is no camera, the user refuses it or the page is not on
 * HTTPS, it says so; the caller's manual barcode field stays the way in.
 */
export default function BarcodeScannerDialog({ open, onOpenChange, onDetected }: Props) {
  const { t } = useTranslation()

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[calc(100%-2rem)] max-w-[440px] sm:max-w-[440px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Camera className="size-4 text-primary" />
            {t('barcodeScanner.title')}
          </DialogTitle>
          <DialogDescription>{t('barcodeScanner.hint')}</DialogDescription>
        </DialogHeader>
        {/* Radix mounts the content only while the dialog is open, so every opening starts the camera afresh. */}
        <Scanner onDetected={onDetected} />
        <DialogFooter>
          <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>
            {t('barcodeScanner.close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
