import { useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { getProduct, getProductReviews } from '../api/productsApi'
import { getProductEstimate } from '../api/pricesApi'
import { getBottle } from '../api/bottlesApi'
import { useAuth } from '../contexts/useAuth'
import type { ProductDetail, ProductListing, ProductOwner } from '../types'
import { CATEGORY_COLORS } from '../lib/bottlePalette'
import NavBar from '../components/NavBar'
import Avatar from '../components/Avatar'
import BottleDetailPanel from '../components/BottleDetailPanel'
import PriceEstimateView from '../components/PriceEstimateView'
import ReviewsAggregate from '../components/reviews/ReviewsAggregate'
import ReviewCard from '../components/reviews/ReviewCard'
import { BottleSvg } from '../components/BarShelf'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

const REVIEWS_PAGE_SIZE = 10

const isNotFound = (error: unknown) => isAxiosError(error) && error.response?.status === 404

/** The bottle opened in the panel, plus its owner: the panel invalidates `['bottles', ownerId]`. */
interface OpenBottle {
  bottleId: string
  ownerId: string
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mt-10">
      <h2 className="mb-4 text-xs font-medium uppercase tracking-widest text-muted-foreground">{title}</h2>
      {children}
    </section>
  )
}

function Fact({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs uppercase tracking-wide text-muted-foreground">{label}</span>
      <span className="text-base text-foreground">{value}</span>
    </div>
  )
}

function ProductHeader({ product }: { product: ProductDetail }) {
  const { t } = useTranslation()
  const cat = CATEGORY_COLORS[product.category]
  const maker = [product.brand, product.distilleryName !== product.brand ? product.distilleryName : null]
    .filter(Boolean)
    .join(' · ')
  const origin = [product.region, product.country].filter(Boolean).join(', ')

  return (
    <div className="grid gap-8 md:grid-cols-[280px_1fr]">
      <div
        className="flex h-72 items-center justify-center overflow-hidden rounded-xl border border-border"
        style={{ background: `radial-gradient(ellipse at 50% 45%, ${cat.glow}22, var(--background))` }}
      >
        {product.imageUrl ? (
          <img src={product.imageUrl} alt={product.name} className="size-full object-contain p-4" />
        ) : (
          <div className="w-[80px]">
            <BottleSvg category={product.category} condition="Sealed" />
          </div>
        )}
      </div>

      <div>
        <Badge
          variant="outline"
          style={{ backgroundColor: `${cat.glass}22`, borderColor: `${cat.glass}66`, color: cat.glass }}
        >
          {cat.label}
        </Badge>
        <h1 className="mt-3 font-heading text-3xl font-bold leading-tight text-primary sm:text-4xl">
          {product.name}
        </h1>
        {maker && <div className="mt-1.5 text-lg italic text-primary/85">{maker}</div>}

        {(product.age != null || product.abvPercent != null || product.volumeMl != null || origin) && (
          <div className="mt-6 grid grid-cols-[repeat(auto-fit,minmax(110px,1fr))] gap-x-6 gap-y-4 rounded-md border border-primary/10 bg-primary/[0.04] p-5">
            {product.age != null && <Fact label={t('bottle.age')} value={t('products.ageSuffix', { age: product.age })} />}
            {product.abvPercent != null && <Fact label={t('bottle.abv')} value={`${product.abvPercent}%`} />}
            {product.volumeMl != null && (
              <Fact label={t('bottle.volume')} value={t('products.volumeSuffix', { volume: product.volumeMl })} />
            )}
            {origin && <Fact label={t('bottle.origin')} value={origin} />}
          </div>
        )}

        {product.description && (
          <p className="mt-6 text-base italic leading-relaxed text-foreground/90">{product.description}</p>
        )}
      </div>
    </div>
  )
}

function ListingRow({
  listing,
  opening,
  onOpen,
}: {
  listing: ProductListing
  opening: boolean
  onOpen: () => void
}) {
  const { t } = useTranslation()

  return (
    <li className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
      <Link to={`/bar/${listing.sellerId}`} className="flex min-w-0 items-center gap-2.5 hover:text-primary">
        <Avatar displayName={listing.sellerDisplayName} avatarUrl={listing.sellerAvatarUrl} size={32} />
        <span className="truncate text-sm font-medium">{listing.sellerDisplayName}</span>
      </Link>

      <div className="flex flex-wrap items-center gap-1.5">
        <Badge variant="outline">{t(`addBottle.condition${listing.condition}`)}</Badge>
        {listing.vintageYear != null && (
          <Badge variant="outline">
            {t('bottle.vintage')} {listing.vintageYear}
          </Badge>
        )}
        {listing.forSaleQuantity > 1 && (
          <Badge variant="outline" className="border-primary/40 text-primary">
            ×{listing.forSaleQuantity}
          </Badge>
        )}
      </div>

      <div className="ml-auto flex items-center gap-3">
        <span className="font-heading text-base font-semibold text-primary">
          {listing.askingPrice != null
            ? `${listing.currency ?? ''} ${listing.askingPrice.toLocaleString()}`.trim()
            : t('marketplace.priceOnRequest')}
          {listing.askingPrice != null && listing.forSaleQuantity > 1 && (
            <span className="ml-1 text-xs font-normal text-muted-foreground">{t('bottle.perBottle')}</span>
          )}
        </span>
        <Button size="sm" variant="outline" onClick={onOpen} disabled={opening}>
          {opening ? t('product.opening') : t('product.open')}
        </Button>
      </div>
    </li>
  )
}

function OwnerChip({ owner }: { owner: ProductOwner }) {
  return (
    <Link
      to={`/bar/${owner.userId}`}
      className="flex items-center gap-2.5 rounded-md border border-border px-3 py-2 transition-colors hover:border-primary/40"
    >
      <Avatar displayName={owner.displayName} avatarUrl={owner.avatarUrl} size={32} />
      <span className="min-w-0 flex-1 truncate text-sm">{owner.displayName}</span>
      {owner.bottleCount > 1 && <span className="text-xs text-primary">×{owner.bottleCount}</span>}
    </Link>
  )
}

function ProductReviews({ product }: { product: ProductDetail }) {
  const { t } = useTranslation()
  // The page starts with the newest 5 from the detail; "show more" switches to the paged endpoint from
  // page 1, so its first page replaces those 5 instead of appending duplicates.
  const [paged, setPaged] = useState(false)

  const reviewsQuery = useInfiniteQuery({
    queryKey: ['products', product.id, 'reviews'],
    queryFn: ({ pageParam }) => getProductReviews(product.id, pageParam, REVIEWS_PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (last, all) => (last.hasMore ? all.length + 1 : undefined),
    enabled: paged,
  })

  const reviews = paged && reviewsQuery.data
    ? reviewsQuery.data.pages.flatMap(p => p.items)
    : product.latestReviews

  const canShowMore = paged
    ? reviewsQuery.hasNextPage || reviewsQuery.isFetching
    : product.reviewsCount > product.latestReviews.length

  if (product.reviewsCount === 0) return null

  return (
    <Section title={t('product.reviews')}>
      <div className="flex flex-col gap-3">
        {reviews.map(review => (
          <ReviewCard key={review.id} review={review} />
        ))}
      </div>

      {reviewsQuery.isError && <p className="mt-4 text-sm text-destructive">{t('product.error')}</p>}

      {canShowMore && (
        <div className="mt-5 flex justify-center">
          <Button
            variant="outline"
            disabled={reviewsQuery.isFetching}
            onClick={() => (paged ? reviewsQuery.fetchNextPage() : setPaged(true))}
          >
            {reviewsQuery.isFetching ? t('catalog.loadingMore') : t('product.showMore')}
          </Button>
        </div>
      )}
    </Section>
  )
}

export default function ProductPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { user, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const [openBottle, setOpenBottle] = useState<OpenBottle | null>(null)

  const productQuery = useQuery({
    queryKey: ['products', id],
    queryFn: () => getProduct(id),
    // A missing product will not appear on a retry.
    retry: (failureCount, error) => !isNotFound(error) && failureCount < 3,
  })
  const product = productQuery.data

  // /api/prices is [Authorize]: an anonymous call would 401 and redirect to /login.
  const estimateQuery = useQuery({
    queryKey: ['prices', 'product', id],
    queryFn: () => getProductEstimate(id),
    enabled: isAuthenticated && product != null,
  })

  // Keyed under the owner's bottles, so the panel's own invalidations (like, offer, …) refresh it too.
  const bottleQuery = useQuery({
    queryKey: ['bottles', openBottle?.ownerId, openBottle?.bottleId],
    queryFn: () => getBottle(openBottle!.bottleId),
    enabled: openBottle != null,
  })

  const closePanel = () => {
    setOpenBottle(null)
    queryClient.invalidateQueries({ queryKey: ['products', id] })
  }

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[1100px] px-6 py-10">
        <Link to="/catalog" className="mb-6 inline-block text-sm text-muted-foreground hover:text-primary">
          {t('product.backToCatalog')}
        </Link>

        {productQuery.isPending && (
          <div className="grid gap-8 md:grid-cols-[280px_1fr]">
            <Skeleton className="h-72 rounded-xl" />
            <div className="space-y-4">
              <Skeleton className="h-6 w-24" />
              <Skeleton className="h-10 w-3/4" />
              <Skeleton className="h-24 w-full" />
            </div>
          </div>
        )}

        {productQuery.isError && (
          <div className="mx-auto max-w-md py-16 text-center">
            {isNotFound(productQuery.error) ? (
              <>
                <h1 className="font-heading text-2xl font-semibold text-primary">{t('product.notFound')}</h1>
                <p className="mt-2 text-muted-foreground">{t('product.notFoundHint')}</p>
              </>
            ) : (
              <p className="text-destructive">{t('product.error')}</p>
            )}
            <Button asChild variant="outline" className="mt-6">
              <Link to="/catalog">{t('product.backToCatalog')}</Link>
            </Button>
          </div>
        )}

        {product && (
          <>
            <ProductHeader product={product} />

            <Section title={t('product.community')}>
              <ReviewsAggregate
                averageScore={product.averageScore}
                reviewsCount={product.reviewsCount}
                topFlavors={product.topFlavors}
                emptyLabel={t('product.noReviews')}
              />
              {isAuthenticated && (
                <PriceEstimateView estimate={estimateQuery.data} isLoading={estimateQuery.isLoading} />
              )}
            </Section>

            <Section title={t('product.forSale')}>
              {product.listings.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t('product.nobodySelling')}</p>
              ) : (
                <Card className="gap-0 p-0">
                  <ul className="divide-y divide-border">
                    {product.listings.map(listing => (
                      <ListingRow
                        key={listing.bottleId}
                        listing={listing}
                        opening={bottleQuery.isFetching && openBottle?.bottleId === listing.bottleId}
                        onOpen={() => setOpenBottle({ bottleId: listing.bottleId, ownerId: listing.sellerId })}
                      />
                    ))}
                  </ul>
                </Card>
              )}
            </Section>

            <Section title={t('product.owners')}>
              {product.owners.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t('product.noOwners')}</p>
              ) : (
                <>
                  <div className="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-2.5">
                    {product.owners.map(owner => (
                      <OwnerChip key={owner.userId} owner={owner} />
                    ))}
                  </div>
                  {product.ownersCount > product.owners.length && (
                    <p className="mt-3 text-sm text-muted-foreground">
                      {t('product.andMore', { count: product.ownersCount - product.owners.length })}
                    </p>
                  )}
                </>
              )}
            </Section>

            <ProductReviews key={product.id} product={product} />
          </>
        )}
      </main>

      {openBottle && bottleQuery.data && (
        <BottleDetailPanel
          bottle={bottleQuery.data}
          userId={openBottle.ownerId}
          currentUserId={user?.id ?? ''}
          onClose={closePanel}
        />
      )}
    </div>
  )
}
