import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath } from 'node:url'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    // Long-lived vendor chunks, so a deploy that only changes app code leaves these cached in the browser.
    // Vite 8 bundles with Rolldown, which has no object form of Rollup's manualChunks: each group matches
    // module paths instead, so it lists the packages' own dependencies too (scheduler, react-router,
    // query-core, the @radix-ui/* packages) that the object form used to pull in implicitly.
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: 'react-vendor',
              test: /[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/,
            },
            { name: 'query-vendor', test: /[\\/]node_modules[\\/]@tanstack[\\/]/ },
            { name: 'radix-vendor', test: /[\\/]node_modules[\\/](radix-ui|@radix-ui)[\\/]/ },
            {
              name: 'i18n-vendor',
              test: /[\\/]node_modules[\\/](i18next|react-i18next|i18next-browser-languagedetector)[\\/]/,
            },
          ],
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5000',
      '/uploads': 'http://localhost:5000',
    },
  },
})
