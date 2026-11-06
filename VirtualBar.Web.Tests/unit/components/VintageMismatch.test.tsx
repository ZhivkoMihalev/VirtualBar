import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { getProduct } from '@/api/productsApi'
import VintageMismatch from '@/components/VintageMismatch'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import { productDetail } from '../fixtures/products'
import { networkError, renderWithProviders } from '../testUtils'

vi.mock('@/api/productsApi')

const millesime1988 = { id: 'millesime-1988', name: 'Millésime 1988', vintageYear: 1988 }

function renderMismatch(year: number) {
  const onLink = vi.fn()
  const onRequest = vi.fn()
  renderWithProviders(<VintageMismatch product={millesime1988} year={year} onLink={onLink} onRequest={onRequest} />)
  return { onLink, onRequest }
}

describe('VintageMismatch (#59)', () => {
  it("says the bottle's vintage is not the product's", async () => {
    vi.mocked(getProduct).mockResolvedValue(productDetail({ ...millesime1988, otherYears: [] }))

    renderMismatch(1983)

    expect(
      screen.getByText(i18n.t('addBottle.vintageMismatch', { productYear: 1988, year: 1983 })),
    ).toBeInTheDocument()
    expect(await screen.findByText(bg.addBottle.vintageMismatchHint)).toBeInTheDocument()
  })

  it("links the vintage the product's line already has", async () => {
    const sibling = productDetail({ id: 'millesime-1979', name: 'Millésime 1979', vintageYear: 1979 })
    vi.mocked(getProduct).mockImplementation(async id =>
      id === millesime1988.id
        ? productDetail({
            ...millesime1988,
            otherYears: [{ id: sibling.id, name: sibling.name, year: 1979, averageScore: null, listingsCount: 0 }],
          })
        : sibling,
    )
    const { onLink, onRequest } = renderMismatch(1979)

    await userEvent.click(
      await screen.findByRole('button', { name: i18n.t('addBottle.linkVintage', { name: 'Millésime 1979' }) }),
    )

    await waitFor(() => expect(onLink).toHaveBeenCalledExactlyOnceWith(sibling))
    expect(onRequest).not.toHaveBeenCalled()
  })

  it('offers to request a vintage the line does not have, named after the line', async () => {
    vi.mocked(getProduct).mockResolvedValue(productDetail({ ...millesime1988, otherYears: [] }))
    const { onRequest } = renderMismatch(1983)

    await userEvent.click(
      await screen.findByRole('button', { name: i18n.t('addBottle.requestVintage', { year: 1983 }) }),
    )

    expect(onRequest).toHaveBeenCalledExactlyOnceWith('Millésime 1983')
  })

  it('still offers the request when the line cannot load', async () => {
    vi.mocked(getProduct).mockRejectedValue(networkError())

    renderMismatch(1983)

    expect(
      await screen.findByRole('button', { name: i18n.t('addBottle.requestVintage', { year: 1983 }) }),
    ).toBeInTheDocument()
  })

  it('keeps the warning when the vintage to link cannot load', async () => {
    vi.mocked(getProduct)
      .mockResolvedValueOnce(
        productDetail({
          ...millesime1988,
          otherYears: [{ id: 'millesime-1979', name: 'Millésime 1979', year: 1979, averageScore: null, listingsCount: 0 }],
        }),
      )
      .mockRejectedValue(networkError())
    const { onLink } = renderMismatch(1979)

    const link = await screen.findByRole('button', { name: i18n.t('addBottle.linkVintage', { name: 'Millésime 1979' }) })
    await userEvent.click(link)

    await waitFor(() => expect(link).toBeEnabled())
    expect(onLink).not.toHaveBeenCalled()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })
})
