import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import { getProduct } from '../api/productsApi'
import type { Product } from '../types'
import { withYear } from '../lib/productYears'
import { Button } from '@/components/ui/button'

interface VintageMismatchProps {
  /** The linked catalog product, a vintage-dated one. */
  product: { id: string; name: string; vintageYear: number }
  /** The vintage the collector gave the bottle, a year other than the product's. */
  year: number
  /** Links the bottle to the product of that vintage, when the catalog has it. */
  onLink: (product: Product) => void
  /** Unlinks the bottle and names it after the missing vintage, so saving files a request for it. */
  onRequest: (name: string) => void
}

/**
 * Warns that the bottle's vintage is not its linked product's (#59), without blocking the save. When the product's
 * line has that vintage, it offers to link it; otherwise it offers to request it in one step.
 */
export default function VintageMismatch({ product, year, onLink, onRequest }: VintageMismatchProps) {
  const { t } = useTranslation()
  const [linking, setLinking] = useState(false)

  // The product page's own query: its line (otherYears) holds the vintages the catalog already has.
  const line = useQuery({ queryKey: ['products', product.id], queryFn: () => getProduct(product.id) })
  const sibling = line.data?.otherYears.find(y => y.year === year)

  const link = async (id: string) => {
    setLinking(true)
    try {
      onLink(await getProduct(id))
    } catch {
      // The warning stays; the collector can still save as it is or try again.
    } finally {
      setLinking(false)
    }
  }

  return (
    <div role="status" className="space-y-2 rounded-md border border-primary/40 bg-primary/5 px-3 py-2.5 text-sm">
      <p className="flex gap-2">
        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden />
        <span>{t('addBottle.vintageMismatch', { productYear: product.vintageYear, year })}</span>
      </p>
      {!line.isPending && (
        <div className="flex flex-wrap items-center gap-2">
          {sibling ? (
            <Button type="button" size="sm" variant="secondary" disabled={linking} onClick={() => link(sibling.id)}>
              {t('addBottle.linkVintage', { name: sibling.name })}
            </Button>
          ) : (
            <Button
              type="button"
              size="sm"
              variant="secondary"
              onClick={() => onRequest(withYear(product.name, product.vintageYear, year))}
            >
              {t('addBottle.requestVintage', { year })}
            </Button>
          )}
          <span className="text-xs text-muted-foreground">{t('addBottle.vintageMismatchHint')}</span>
        </div>
      )}
    </div>
  )
}
