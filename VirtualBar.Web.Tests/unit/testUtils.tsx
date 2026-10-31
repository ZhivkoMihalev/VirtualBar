import type { ReactElement } from 'react'
import { render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { AxiosError, AxiosHeaders } from 'axios'
import type { ApiErrorBody } from '@/types'
import '@/i18n'

/**
 * Renders a component inside the providers the app gives it: a fresh query client (no retries, so a failed request
 * shows its error at once) and a router opened at `route`. i18n starts in Bulgarian, as on a first visit.
 */
export function renderWithProviders(ui: ReactElement, { route = '/' }: { route?: string } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
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
