import { vi } from 'vitest'

/**
 * A stand-in for the camera behind `@zxing/browser`, which jsdom does not have. A test file mocks the module with
 * `zxingBrowserMock` and then decides what the camera does: read a barcode, never read one, or fail to start.
 */
export const camera = {
  /** The reader's `decodeFromConstraints`; each test gives it an implementation. */
  decode: vi.fn(),
  /** What the scanner calls to turn the camera off. */
  stop: vi.fn(),
}

/** The mocked `@zxing/browser`: only the reader the app uses. */
export const zxingBrowserMock = () => ({
  BrowserMultiFormatReader: class {
    decodeFromConstraints(...args: unknown[]) {
      return camera.decode(...args)
    }
  },
})

type DecodeCallback = (result: { getText: () => string } | undefined, error: unknown, controls: { stop: () => void }) => void

/** The camera starts and reads `barcode` right away. */
export function cameraReads(barcode: string) {
  camera.decode.mockImplementation(async (_constraints: unknown, _video: unknown, callback: DecodeCallback) => {
    setTimeout(() => callback({ getText: () => barcode }, undefined, { stop: camera.stop }))
    return { stop: camera.stop }
  })
}

/** The camera starts and keeps looking without reading anything. */
export function cameraSeesNothing() {
  camera.decode.mockResolvedValue({ stop: camera.stop })
}

/** The camera does not start, as the browser reports it (`NotAllowedError`, `NotFoundError`…). */
export function cameraFails(name: string) {
  camera.decode.mockRejectedValue(new DOMException('camera', name))
}

/** Whether the page may ask for a camera: browsers give `navigator.mediaDevices` only on HTTPS. */
export function setCameraSupported(supported: boolean) {
  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true,
    value: supported ? { getUserMedia: vi.fn() } : undefined,
  })
}
