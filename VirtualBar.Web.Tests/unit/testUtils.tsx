import { useEffect, type ReactElement } from 'react'
import { render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { AxiosError, AxiosHeaders } from 'axios'
import { TOKEN_KEY, USER_KEY } from '@/api/client'
import { TooltipProvider } from '@/components/ui/tooltip'
import { AuthProvider } from '@/contexts/AuthContext'
import { ChatProvider } from '@/contexts/ChatContext'
import type { ApiErrorBody, User } from '@/types'
import '@/i18n'

/** Reports the router's current path and query string, so a test can check what a page wrote into the URL. */
function LocationProbe({ onChange }: { onChange: (location: string) => void }) {
  const location = useLocation()
  useEffect(() => {
    onChange(location.pathname + location.search)
  }, [location, onChange])
  return null
}

/**
 * Renders a component inside the providers App.tsx gives it (auth, router, chat, tooltips) with a fresh query client
 * and the router opened at `route`. Queries do not retry unless the component asks to, and a retry it asks for
 * happens at once. i18n starts in Bulgarian, as on a first visit. Pass `path` (e.g. `/product/:id`) for a page that
 * reads route parameters. `location()` returns the current path and query string.
 */
export function renderWithProviders(
  ui: ReactElement,
  { route = '/', path }: { route?: string; path?: string } = {},
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, retryDelay: 0 }, mutations: { retry: false } },
  })
  let current = route

  const result = render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <MemoryRouter initialEntries={[route]}>
          <ChatProvider>
            <TooltipProvider>
              {path ? (
                <Routes>
                  <Route path={path} element={ui} />
                </Routes>
              ) : (
                ui
              )}
              <LocationProbe onChange={location => (current = location)} />
            </TooltipProvider>
          </ChatProvider>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  )

  return { ...result, location: () => current }
}

export const signedInUser: User = {
  id: '99999999-9999-9999-9999-999999999999',
  email: 'collector@example.com',
  displayName: 'Collector',
  createdAt: '2026-10-31T10:00:00Z',
  isAdmin: false,
}

/** Saves a session the way a login does; render afterwards, since AuthProvider reads it while it mounts. */
export function signIn(user: User = signedInUser) {
  localStorage.setItem(TOKEN_KEY, 'jwt-token')
  localStorage.setItem(USER_KEY, JSON.stringify(user))
}

/** The error axios rejects with when the API answers `status`. */
export function apiError(status: number, data: ApiErrorBody = {}): AxiosError<ApiErrorBody> {
  const config = { headers: new AxiosHeaders() }
  return new AxiosError(`Request failed with status code ${status}`, AxiosError.ERR_BAD_REQUEST, config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data,
  })
}

/** The error axios rejects with when no answer arrives at all. */
export function networkError(): AxiosError<ApiErrorBody> {
  return new AxiosError('Network Error', AxiosError.ERR_NETWORK)
}

/** A promise the test settles itself, to look at a component while its request is still pending. */
export function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}
