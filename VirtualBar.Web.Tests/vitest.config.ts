import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Same alias as the app's vite.config.ts, so tests import the code under test exactly as the app does.
    alias: {
      '@': fileURLToPath(new URL('../VirtualBar.Web/src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    include: ['unit/**/*.test.{ts,tsx}'],
    setupFiles: ['./unit/setup.ts'],
    mockReset: true,
    restoreMocks: true,
  },
})
