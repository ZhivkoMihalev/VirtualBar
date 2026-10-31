import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Search } from 'lucide-react'
import { browseProducts } from '../api/productsApi'
import type { ProductCard, SpiritCategory } from '../types'
import { CATEGORY_COLORS } from '../lib/bottlePalette'
import NavBar from '../components/NavBar'
import CategoryPill from '../components/CategoryPill'
import { BottleSvg } from '../components/BarShelf'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]

const PAGE_SIZE = 24

function parseCategory(value: string | null): SpiritCategory | undefined {
  return CATEGORIES.find(c => c === value)
}

/**
 * The term the catalog is actually queried with: the typed term once it has been stable for `delay`
 * ms. A single character is never sent (the API rejects it), so the last valid term — and its results
 * — stay in place until the user types a second character or clears the box.
 */
function useSettledTerm(raw: string, delay: number): string {
  const [settled, setSettled] = useState(raw.length === 1 ? '' : raw)

  useEffect(() => {
    if (raw.length === 1) return
    const handle = setTimeout(() => setSettled(raw), delay)
    return () => clearTimeout(handle)
  }, [raw, delay])

  return settled
}

function ProductCardView({ product }: { product: ProductCard }) {
  const { t } = useTranslation()
  const cat = CATEGORY_COLORS[product.category]
  // The distillery is worth a second mention only when the label says something else.
  const maker = [product.brand, product.distilleryName !== product.brand ? product.distilleryName : null]
    .filter(Boolean)
    .join(' · ')
  const hasActivity = product.reviewsCount > 0 || product.ownersCount > 0 || product.listingsCount > 0

  return (
    <Link to={`/product/${product.id}`} className="group block h-full rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
      <Card className="flex h-full flex-col gap-0 overflow-hidden p-0 transition-all group-hover:-translate-y-0.5 group-hover:ring-primary/30">
        <div
          className="flex h-40 items-center justify-center overflow-hidden"
          style={{ background: `radial-gradient(ellipse at 50% 40%, ${cat.glow}1A, var(--background))` }}
        >
          {product.imageUrl ? (
            <img src={product.imageUrl} alt={product.name} loading="lazy" className="size-full object-contain p-3" />
          ) : (
            <div className="w-[50px]">
              <BottleSvg category={product.category} condition="Sealed" />
            </div>
          )}
        </div>

        <div className="flex flex-1 flex-col p-4">
          <div className="mb-2.5 flex flex-wrap items-center gap-2">
            <Badge
              variant="outline"
              style={{ backgroundColor: `${cat.glass}22`, borderColor: `${cat.glass}55`, color: cat.glass }}
            >
              {cat.label}
            </Badge>
            {product.age != null && (
              <Badge variant="outline" className="text-muted-foreground">
                {t('products.ageSuffix', { age: product.age })}
              </Badge>
            )}
            {product.volumeMl != null && (
              <Badge variant="outline" className="text-muted-foreground">
                {t('products.volumeSuffix', { volume: product.volumeMl })}
              </Badge>
            )}
          </div>

          <div className="font-heading text-base font-semibold text-foreground">{product.name}</div>
          {maker && <div className="text-sm italic text-muted-foreground">{maker}</div>}

          {/* Most catalog products have no community activity yet; they get no footer at all rather than an empty rule. */}
          {hasActivity && (
            <div className="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-border pt-3 text-xs text-muted-foreground">
              {product.reviewsCount > 0 && product.averageScore != null && (
                <span className="font-medium text-primary">
                  ★ {product.averageScore}{' '}
                  <span className="font-normal text-muted-foreground">
                    ({t('catalog.reviews', { count: product.reviewsCount })})
                  </span>
                </span>
              )}
              {product.ownersCount > 0 && <span>{t('catalog.owners', { count: product.ownersCount })}</span>}
              {product.listingsCount > 0 && (
                <span className="text-primary">{t('catalog.forSale', { count: product.listingsCount })}</span>
              )}
            </div>
          )}
        </div>
      </Card>
    </Link>
  )
}

export default function CatalogPage() {
  const { t } = useTranslation()
  // The URL is the state, so back/forward and shared links reproduce the same view.
  const [searchParams, setSearchParams] = useSearchParams()
  const typed = searchParams.get('q') ?? ''
  const category = parseCategory(searchParams.get('category'))
  const term = useSettledTerm(typed.trim(), 300)

  const updateParam = (key: 'q' | 'category', value: string | undefined, replace: boolean) => {
    setSearchParams(
      prev => {
        const next = new URLSearchParams(prev)
        if (value) next.set(key, value)
        else next.delete(key)
        return next
      },
      { replace },
    )
  }

  const {
    data,
    isPending,
    isError,
    isPlaceholderData,
    hasNextPage,
    fetchNextPage,
    isFetchingNextPage,
    isFetchNextPageError,
  } = useInfiniteQuery({
    queryKey: ['products', 'browse', term, category ?? 'all'],
    queryFn: ({ pageParam }) =>
      browseProducts({ search: term || undefined, category, page: pageParam, pageSize: PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (last, all) => (last.hasMore ? all.length + 1 : undefined),
    // Keeps the current cards on screen while a new term or category loads, instead of flashing skeletons.
    placeholderData: keepPreviousData,
  })

  const products = data?.pages.flatMap(p => p.items) ?? []

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[1100px] px-6 py-10">
        <div className="mb-7">
          <div className="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t('catalog.discover')}
          </div>
          <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">{t('catalog.title')}</h1>
          <div className="mt-1.5 text-lg italic text-primary/90">{t('catalog.subtitle')}</div>
        </div>

        <div className="mb-9 space-y-4">
          <div className="max-w-md">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                type="search"
                value={typed}
                // Every keystroke replaces the history entry, so Back leaves the page instead of undoing letters.
                onChange={e => updateParam('q', e.target.value, true)}
                placeholder={t('catalog.searchPlaceholder')}
                aria-label={t('catalog.searchLabel')}
                className="h-10 pl-9"
              />
            </div>
            {typed.trim().length === 1 && (
              <p className="mt-1.5 text-xs text-muted-foreground">{t('catalog.minChars')}</p>
            )}
          </div>

          <div role="group" aria-label={t('catalog.categoryFilter')} className="flex flex-wrap gap-2">
            <CategoryPill
              label={t('catalog.allCategories')}
              active={category === undefined}
              onClick={() => updateParam('category', undefined, false)}
            />
            {CATEGORIES.map(cat => (
              <CategoryPill
                key={cat}
                label={CATEGORY_COLORS[cat].label}
                active={category === cat}
                color={CATEGORY_COLORS[cat].glass}
                onClick={() => updateParam('category', category === cat ? undefined : cat, false)}
              />
            ))}
          </div>
        </div>

        {isPending && (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(240px,1fr))] gap-5">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-80 rounded-xl" />
            ))}
          </div>
        )}

        {isError && !data && (
          <div className="mx-auto max-w-md rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-center text-sm text-destructive">
            {t('catalog.error')}
          </div>
        )}

        {data && products.length === 0 && (
          <div className="py-16 text-center text-muted-foreground">{t('catalog.empty')}</div>
        )}

        {products.length > 0 && (
          <>
            <div
              aria-busy={isPlaceholderData}
              className={cn(
                'grid grid-cols-[repeat(auto-fill,minmax(240px,1fr))] gap-5 transition-opacity',
                isPlaceholderData && 'opacity-60',
              )}
            >
              {products.map(product => (
                <ProductCardView key={product.id} product={product} />
              ))}
            </div>

            {isFetchNextPageError && (
              <p className="mt-6 text-center text-sm text-destructive">{t('catalog.error')}</p>
            )}

            {hasNextPage && !isPlaceholderData && (
              <div className="mt-8 flex justify-center">
                <Button variant="outline" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
                  {isFetchingNextPage ? t('catalog.loadingMore') : t('catalog.loadMore')}
                </Button>
              </div>
            )}
          </>
        )}
      </main>
    </div>
  )
}
