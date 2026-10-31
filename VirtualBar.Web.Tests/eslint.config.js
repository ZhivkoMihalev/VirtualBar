import js from '@eslint/js'
import globals from 'globals'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['playwright-report', 'test-results']),
  {
    files: ['**/*.{js,ts,tsx}'],
    extends: [js.configs.recommended, tseslint.configs.recommended],
    languageOptions: {
      // Unit tests run in jsdom, the E2E specs and the configs in Node.
      globals: { ...globals.browser, ...globals.node },
    },
  },
])
