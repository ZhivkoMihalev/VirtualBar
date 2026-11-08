import { useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { AlertTriangle, Search } from 'lucide-react'
import NavBar from '../components/NavBar'
import { getProductBarcodes, removeProductBarcode } from '../api/productBarcodesApi'
import type { ProductBarcode } from '../types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString()
}

function PairRow({ pair }: { pair: ProductBarcode }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => removeProductBarcode(pair.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['productBarcodes'] })
      toast.success(t('productBarcodes.removed', { barcode: pair.barcode }))
    },
    onError: () => toast.error(t('productBarcodes.error')),
  })

  return (
    <Card>
      <CardContent className="flex flex-wrap items-center gap-x-5 gap-y-3 py-4">
        <div className="min-w-0 flex-1 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-base tracking-wider text-foreground">{pair.barcode}</span>
            {pair.conflict && (
              <Badge variant="destructive" className="gap-1">
                <AlertTriangle className="size-3" />
                {t('productBarcodes.conflict')}
              </Badge>
            )}
          </div>
          <Link to={`/product/${pair.productId}`} className="block truncate text-sm text-primary hover:underline">
            {pair.productBrand ? `${pair.productBrand} · ${pair.productName}` : pair.productName}
          </Link>
          <div className="text-xs text-muted-foreground">
            {t('productBarcodes.addedBy', { name: pair.addedByDisplayName, date: formatDate(pair.createdAt) })}
            {' · '}
            {t('productBarcodes.collectors', { count: pair.collectors })}
          </div>
        </div>

        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="outline" size="sm" disabled={mutation.isPending}>
              {t('productBarcodes.remove')}
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('productBarcodes.removeTitle', { barcode: pair.barcode })}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('productBarcodes.removeText', { product: pair.productName })}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t('productBarcodes.cancel')}</AlertDialogCancel>
              <AlertDialogAction onClick={() => mutation.mutate()}>{t('productBarcodes.remove')}</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </CardContent>
    </Card>
  )
}

/**
 * The barcodes collectors gave catalog products (#49), for an admin: who gave each, how many collectors back it, and
 * which barcodes another product also claims, so a wrong pair can be removed for good.
 */
export default function ProductBarcodesAdminPage() {
  const { t } = useTranslation()
  const [typed, setTyped] = useState('')
  const [term, setTerm] = useState('')

  const { data, isPending, isError, hasNextPage, fetchNextPage, isFetchingNextPage } = useInfiniteQuery({
    queryKey: ['productBarcodes', term],
    queryFn: ({ pageParam }) => getProductBarcodes(term, pageParam),
    initialPageParam: 1,
    getNextPageParam: (last, all) => (last.hasMore ? all.length + 1 : undefined),
    placeholderData: keepPreviousData,
  })

  const pairs = data?.pages.flatMap(p => p.items) ?? []

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[880px] px-6 py-10 sm:px-10">
        <div className="mb-7">
          <div className="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t('nav.productBarcodes')}
          </div>
          <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">{t('productBarcodes.title')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('productBarcodes.subtitle')}</p>
        </div>

        <form
          role="search"
          className="mb-6 flex gap-2"
          onSubmit={e => {
            e.preventDefault()
            setTerm(typed.trim())
          }}
        >
          <Input
            aria-label={t('productBarcodes.searchLabel')}
            placeholder={t('productBarcodes.searchPlaceholder')}
            value={typed}
            onChange={e => setTyped(e.target.value)}
            className="h-9 flex-1"
          />
          <Button type="submit" variant="secondary" className="h-9">
            <Search className="size-3.5" />
            {t('productBarcodes.search')}
          </Button>
        </form>

        {isPending ? (
          <p className="py-10 text-center text-muted-foreground">{t('productBarcodes.loading')}</p>
        ) : isError ? (
          <p role="alert" className="py-10 text-center text-destructive">
            {t('productBarcodes.error')}
          </p>
        ) : pairs.length === 0 ? (
          <p className="py-10 text-center text-muted-foreground">{t('productBarcodes.empty')}</p>
        ) : (
          <div className="flex flex-col gap-3">
            {pairs.map(pair => (
              <PairRow key={pair.id} pair={pair} />
            ))}
            {hasNextPage && (
              <Button
                variant="outline"
                className="self-center"
                disabled={isFetchingNextPage}
                onClick={() => fetchNextPage()}
              >
                {t('productBarcodes.showMore')}
              </Button>
            )}
          </div>
        )}
      </main>
    </div>
  )
}
