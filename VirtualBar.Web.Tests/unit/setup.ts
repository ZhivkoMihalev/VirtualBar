import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Vitest runs without globals, so Testing Library cannot register its own cleanup: unmount here, and start every
// test with no saved session or language.
afterEach(() => {
  cleanup()
  localStorage.clear()
})
