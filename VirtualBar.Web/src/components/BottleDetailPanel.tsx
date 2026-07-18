import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { AxiosError } from 'axios'
import { Heart, X, ChevronDown, ChevronUp } from 'lucide-react'
import {
  toggleBottleLike,
  getBottleComments,
  addBottleComment,
  deleteBottleComment,
  listBottleForSale,
  unlistBottleFromSale,
  removeBottle,
} from '../api/bottlesApi'
import { createOffer } from '../api/offersApi'
import { getBottleEstimate } from '../api/pricesApi'
import { getReviews, addReview, updateReview, deleteReview } from '../api/reviewsApi'
import { useAuth } from '../contexts/AuthContext'
import type { Bottle, PriceConfidence, BottleReview, BottleReviewsSummary, FlavorTag, ReviewPayload } from '../types'
import Avatar from './Avatar'
import { CATEGORY_COLORS, BottleSvg } from './BarShelf'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'

const CURRENCIES = ['USD', 'EUR', 'GBP', 'BGN', 'CHF', 'JPY', 'CAD', 'AUD']

const QUICK_SCORES = [70, 80, 85, 90, 95]

const NOTE_FIELDS = ['nose', 'palate', 'finish', 'summary'] as const
type NoteField = (typeof NOTE_FIELDS)[number]

const FLAVOR_TAGS: FlavorTag[] = [
  'Smoky', 'Peaty', 'Medicinal', 'Maritime', 'Vanilla', 'Caramel', 'Toffee',
  'Honey', 'Chocolate', 'Coffee', 'Nutty', 'Malty', 'Creamy', 'Fruity',
  'Citrus', 'TropicalFruit', 'DriedFruit', 'Berry', 'Floral', 'Herbal',
  'Grassy', 'Spicy', 'Pepper', 'Cinnamon', 'Oak', 'Sherry', 'Leather', 'Tobacco',
]

function hasAnyNote(review: BottleReview): boolean {
  return NOTE_FIELDS.some(field => (review[field] ?? '').trim() !== '')
}

function formatRelativeTime(iso: string, t: TFunction): string {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)

  if (seconds < 60) return t('bottle.justNow')

  const units: { limit: number; div: number; key: string }[] = [
    { limit: 3600, div: 60, key: 'minutesAgo' },
    { limit: 86400, div: 3600, key: 'hoursAgo' },
    { limit: 604800, div: 86400, key: 'daysAgo' },
    { limit: 2592000, div: 604800, key: 'weeksAgo' },
    { limit: 31536000, div: 2592000, key: 'monthsAgo' },
    { limit: Infinity, div: 31536000, key: 'yearsAgo' },
  ]

  for (const unit of units) {
    if (seconds < unit.limit) {
      const count = Math.floor(seconds / unit.div)
      return t(`bottle.${unit.key}`, { count })
    }
  }

  return t('bottle.justNow')
}

function LikesSection({ bottle, userId }: { bottle: Bottle; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => toggleBottleLike(bottle.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bottles', userId] })
    },
  })

  const liked = bottle.likedByMe

  return (
    <div className="mt-1 flex items-center gap-3 border-t border-primary/10 pt-6">
      <Button
        variant="ghost"
        onClick={() => mutation.mutate()}
        disabled={mutation.isPending}
        aria-label={liked ? 'Unlike' : 'Like'}
        className="gap-2 px-2"
      >
        <Heart className={cn('size-5', liked ? 'fill-primary text-primary' : 'text-primary')} />
        <span className="text-sm">{t('bottle.likes', { count: bottle.likesCount })}</span>
      </Button>
    </div>
  )
}

function CommentsSection({ bottle, currentUserId }: { bottle: Bottle; currentUserId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState('')

  const { data: comments = [], isLoading, isError } = useQuery({
    queryKey: ['comments', bottle.id],
    queryFn: () => getBottleComments(bottle.id),
  })

  const addMutation = useMutation({
    mutationFn: (content: string) => addBottleComment(bottle.id, content),
    onSuccess: () => {
      setDraft('')
      queryClient.invalidateQueries({ queryKey: ['comments', bottle.id] })
      queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (commentId: string) => deleteBottleComment(bottle.id, commentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comments', bottle.id] })
      queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
    },
  })

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    const content = draft.trim()
    if (!content) return
    addMutation.mutate(content)
  }

  return (
    <div className="mt-6 border-t border-primary/10 pt-6">
      <div className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {t('bottle.comments')}
      </div>

      <div className="flex max-h-80 flex-col gap-3.5 overflow-y-auto">
        {isLoading && <div className="text-sm text-muted-foreground">{t('bottle.loadingComments')}</div>}

        {isError && <div className="text-sm text-destructive">{t('bottle.errorComments')}</div>}

        {!isLoading && !isError && comments.length === 0 && (
          <div className="text-sm italic text-muted-foreground">{t('bottle.noComments')}</div>
        )}

        {!isLoading && !isError &&
          comments.map(comment => (
            <div key={comment.id} className="flex flex-col gap-1">
              <div className="flex items-baseline justify-between gap-2">
                <div className="flex items-baseline gap-2">
                  <span className="text-sm font-medium text-primary">{comment.userDisplayName}</span>
                  <span className="text-xs text-muted-foreground">
                    {formatRelativeTime(comment.createdAt, t)}
                  </span>
                </div>
                {comment.userId === currentUserId && (
                  <Button
                    variant="ghost"
                    size="icon-xs"
                    onClick={() => deleteMutation.mutate(comment.id)}
                    disabled={deleteMutation.isPending}
                    aria-label="Delete comment"
                  >
                    <X className="size-3.5" />
                  </Button>
                )}
              </div>
              <p className="text-sm leading-relaxed text-foreground">{comment.content}</p>
            </div>
          ))}
      </div>

      <form onSubmit={handleSubmit} className="mt-4">
        <Textarea
          value={draft}
          onChange={e => setDraft(e.target.value)}
          rows={2}
          placeholder={t('bottle.commentPlaceholder')}
          className="mb-2.5"
        />
        {addMutation.isError && (
          <div className="mb-2.5 text-sm text-destructive">{t('bottle.errorComment')}</div>
        )}
        <Button type="submit" size="sm" disabled={addMutation.isPending || !draft.trim()}>
          {addMutation.isPending ? t('bottle.posting') : t('bottle.post')}
        </Button>
      </form>
    </div>
  )
}

function ReviewsAggregate({ summary }: { summary: BottleReviewsSummary }) {
  const { t } = useTranslation()
  const avg = summary.averageScore

  return (
    <div className="mb-5 flex flex-wrap items-center gap-x-6 gap-y-3 rounded-md border border-primary/15 bg-primary/[0.04] p-4">
      <div className="flex flex-col items-center">
        <div className="flex items-baseline gap-1">
          {avg != null ? (
            <>
              <span className="font-heading text-4xl font-bold text-primary">{avg.toFixed(1)}</span>
              <span className="text-sm text-muted-foreground">/100</span>
            </>
          ) : (
            <span className="font-heading text-4xl font-bold text-muted-foreground">—</span>
          )}
        </div>
        {avg != null && (
          <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
            {t('reviews.average')}
          </span>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <span className="text-sm text-muted-foreground">
          {summary.reviewsCount > 0
            ? t('reviews.count', { count: summary.reviewsCount })
            : t('reviews.invite')}
        </span>
        {summary.topFlavors.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {summary.topFlavors.map(flavor => (
              <Badge key={flavor} variant="outline" className="border-primary/30 text-primary">
                {t(`flavors.${flavor}`)}
              </Badge>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function ReviewCard({ review }: { review: BottleReview }) {
  const { t } = useTranslation()
  const edited = new Date(review.updatedAt).getTime() > new Date(review.createdAt).getTime()

  return (
    <div className="flex flex-col gap-2.5 rounded-md border border-primary/10 bg-primary/[0.03] p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2.5">
          <Avatar displayName={review.userDisplayName} avatarUrl={review.userAvatarUrl} size={32} />
          <div className="flex flex-col">
            <span className="text-sm font-medium text-primary">{review.userDisplayName}</span>
            <span className="text-xs text-muted-foreground">
              {formatRelativeTime(review.createdAt, t)}
              {edited && ` · ${t('reviews.edited')}`}
            </span>
          </div>
        </div>
        <Badge variant="outline" className="border-primary/40 text-primary">
          <span className="font-heading text-sm font-semibold">{review.score}</span>
          <span className="text-muted-foreground">/100</span>
        </Badge>
      </div>

      {hasAnyNote(review) && (
        <div className="flex flex-col gap-1.5">
          {NOTE_FIELDS.map(field => {
            const value = review[field]
            if (!value || !value.trim()) return null
            return (
              <div key={field} className="text-sm leading-relaxed">
                <span className="mr-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t(`reviews.${field}`)}
                </span>
                <span className="text-foreground">{value}</span>
              </div>
            )
          })}
        </div>
      )}

      {review.flavors.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {review.flavors.map(flavor => (
            <Badge key={flavor} variant="secondary">
              {t(`flavors.${flavor}`)}
            </Badge>
          ))}
        </div>
      )}
    </div>
  )
}

function ReviewForm({ bottle, existing }: { bottle: Bottle; existing: BottleReview | null }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const [score, setScore] = useState(existing ? String(existing.score) : '')
  const [notes, setNotes] = useState<Record<NoteField, string>>({
    nose: existing?.nose ?? '',
    palate: existing?.palate ?? '',
    finish: existing?.finish ?? '',
    summary: existing?.summary ?? '',
  })
  const [flavors, setFlavors] = useState<FlavorTag[]>(existing?.flavors ?? [])
  const [notesOpen, setNotesOpen] = useState(existing != null && hasAnyNote(existing))
  const [error, setError] = useState<string | null>(null)

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['reviews', bottle.id] })
    queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
    queryClient.invalidateQueries({ queryKey: ['marketplace'] })
  }

  const buildPayload = (): ReviewPayload => ({
    score: Number(score),
    nose: notes.nose.trim() || null,
    palate: notes.palate.trim() || null,
    finish: notes.finish.trim() || null,
    summary: notes.summary.trim() || null,
    flavors: flavors.length > 0 ? flavors : null,
  })

  const saveMutation = useMutation({
    mutationFn: () =>
      existing
        ? updateReview(bottle.id, existing.id, buildPayload())
        : addReview(bottle.id, buildPayload()),
    onSuccess: () => {
      setError(null)
      invalidate()
    },
    onError: (err: unknown) => {
      const status = (err as AxiosError).response?.status
      if (status === 409) {
        setError(t('reviews.alreadyReviewed'))
        queryClient.invalidateQueries({ queryKey: ['reviews', bottle.id] })
      } else {
        setError(t('reviews.saveError'))
      }
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => (existing ? deleteReview(bottle.id, existing.id) : Promise.resolve()),
    onSuccess: invalidate,
    onError: () => setError(t('reviews.saveError')),
  })

  const scoreNum = Number(score)
  const scoreValid = score !== '' && Number.isInteger(scoreNum) && scoreNum >= 0 && scoreNum <= 100
  const atMax = flavors.length >= 5

  const toggleFlavor = (flavor: FlavorTag) => {
    setFlavors(prev =>
      prev.includes(flavor) ? prev.filter(f => f !== flavor) : prev.length >= 5 ? prev : [...prev, flavor],
    )
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!scoreValid || saveMutation.isPending) return
    saveMutation.mutate()
  }

  return (
    <form onSubmit={handleSubmit} className="mb-5 rounded-md border border-primary/15 bg-primary/[0.04] p-4">
      <div className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {existing ? t('reviews.edit') : t('reviews.write')}
      </div>

      <div className="mb-4">
        <div className="mb-1.5 text-xs uppercase tracking-wide text-muted-foreground">
          {t('reviews.scoreLabel')}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Input
            type="number"
            min={0}
            max={100}
            step={1}
            value={score}
            onChange={e => setScore(e.target.value)}
            aria-label={t('reviews.scoreLabel')}
            className="h-9 w-24"
          />
          <div className="flex flex-wrap gap-1.5">
            {QUICK_SCORES.map(quick => (
              <button
                key={quick}
                type="button"
                aria-pressed={scoreNum === quick}
                onClick={() => setScore(String(quick))}
                className={cn(
                  'rounded-md border px-2.5 py-1 text-xs transition-colors',
                  scoreNum === quick
                    ? 'border-primary bg-primary/15 text-primary'
                    : 'border-border text-muted-foreground hover:border-primary/40',
                )}
              >
                {quick}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="mb-4">
        <button
          type="button"
          aria-expanded={notesOpen}
          onClick={() => setNotesOpen(open => !open)}
          className="mb-2 flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-primary"
        >
          {notesOpen ? <ChevronUp className="size-3.5" /> : <ChevronDown className="size-3.5" />}
          {t('reviews.tastingNoteToggle')}
        </button>
        {notesOpen && (
          <div className="flex flex-col gap-3">
            {NOTE_FIELDS.map(field => (
              <div key={field}>
                <div className="mb-1 text-xs uppercase tracking-wide text-muted-foreground">
                  {t(`reviews.${field}`)}
                </div>
                <Textarea
                  rows={2}
                  maxLength={2000}
                  value={notes[field]}
                  onChange={e => setNotes(prev => ({ ...prev, [field]: e.target.value }))}
                />
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="mb-4">
        <div className="mb-2 flex items-center gap-2 text-xs uppercase tracking-wide text-muted-foreground">
          <span>{t('reviews.flavorsLabel')}</span>
          <span className="text-primary/70">{t('reviews.flavorsMax')}</span>
        </div>
        <div className="flex flex-wrap gap-1.5">
          {FLAVOR_TAGS.map(flavor => {
            const selected = flavors.includes(flavor)
            return (
              <button
                key={flavor}
                type="button"
                onClick={() => toggleFlavor(flavor)}
                disabled={!selected && atMax}
                aria-pressed={selected}
                className={cn(
                  'rounded-full border px-2.5 py-1 text-xs transition-colors',
                  selected
                    ? 'border-primary bg-primary/15 text-primary'
                    : 'border-border text-muted-foreground hover:border-primary/40 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-border',
                )}
              >
                {t(`flavors.${flavor}`)}
              </button>
            )
          })}
        </div>
      </div>

      {error && <div className="mb-3 text-sm text-destructive">{error}</div>}

      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" size="sm" disabled={!scoreValid || saveMutation.isPending}>
          {saveMutation.isPending ? '···' : t('reviews.save')}
        </Button>

        {existing && (
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="border-destructive/40 text-destructive hover:bg-destructive/10"
              >
                {t('reviews.delete')}
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t('reviews.delete')}</AlertDialogTitle>
                <AlertDialogDescription>{t('reviews.deleteConfirm')}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel disabled={deleteMutation.isPending}>
                  {t('reviews.cancel')}
                </AlertDialogCancel>
                <AlertDialogAction
                  variant="destructive"
                  className="bg-destructive text-white hover:bg-destructive/90 dark:bg-destructive dark:hover:bg-destructive/90"
                  disabled={deleteMutation.isPending}
                  onClick={e => {
                    e.preventDefault()
                    deleteMutation.mutate()
                  }}
                >
                  {t('reviews.delete')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}
      </div>
    </form>
  )
}

function ReviewsSection({ bottle }: { bottle: Bottle }) {
  const { t } = useTranslation()
  const { isAuthenticated } = useAuth()

  const { data: summary, isLoading, isError } = useQuery({
    queryKey: ['reviews', bottle.id],
    queryFn: () => getReviews(bottle.id),
  })

  const otherReviews = summary
    ? summary.reviews.filter(review => review.id !== summary.myReview?.id)
    : []

  return (
    <div className="mt-6 border-t border-primary/10 pt-6">
      <div className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {t('reviews.title')}
      </div>

      {isLoading && <div className="text-sm text-muted-foreground">{t('reviews.loading')}</div>}

      {isError && <div className="text-sm text-destructive">{t('reviews.error')}</div>}

      {summary && (
        <>
          <ReviewsAggregate summary={summary} />

          {isAuthenticated && (
            <ReviewForm key={summary.myReview?.id ?? 'new'} bottle={bottle} existing={summary.myReview} />
          )}

          {otherReviews.length > 0 && (
            <div className="flex flex-col gap-3">
              {otherReviews.map(review => (
                <ReviewCard key={review.id} review={review} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}

function SaleSection({ bottle, userId }: { bottle: Bottle; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [price, setPrice] = useState(bottle.askingPrice?.toString() ?? '')
  const [currency, setCurrency] = useState(bottle.currency ?? 'USD')

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['bottles', userId] })
    queryClient.invalidateQueries({ queryKey: ['marketplace'] })
  }

  const listMutation = useMutation({
    mutationFn: () => listBottleForSale(bottle.id, Number(price), currency),
    onSuccess: invalidate,
  })

  const unlistMutation = useMutation({
    mutationFn: () => unlistBottleFromSale(bottle.id),
    onSuccess: invalidate,
  })

  return (
    <div
      className={cn(
        'mb-5 rounded-md border p-4',
        bottle.isForSale ? 'border-success/30 bg-success/10' : 'border-primary/15 bg-primary/[0.04]',
      )}
    >
      <div className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {t('bottle.saleLabel')}
      </div>

      {bottle.isForSale ? (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <span className="font-heading text-xl font-semibold text-success">
            {bottle.currency ?? 'USD'} {bottle.askingPrice?.toLocaleString() ?? '—'}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => unlistMutation.mutate()}
            disabled={unlistMutation.isPending}
            className="border-destructive/40 text-destructive hover:bg-destructive/10"
          >
            {unlistMutation.isPending ? '···' : t('bottle.removeFromSale')}
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            type="number"
            min={0}
            step="0.01"
            value={price}
            onChange={e => setPrice(e.target.value)}
            placeholder={t('bottle.askingPrice')}
            className="h-9 w-36"
          />
          <Select value={currency} onValueChange={setCurrency}>
            <SelectTrigger className="h-9 w-24">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {CURRENCIES.map(c => (
                <SelectItem key={c} value={c}>
                  {c}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            size="sm"
            onClick={() => listMutation.mutate()}
            disabled={listMutation.isPending || !price || Number(price) <= 0}
            className="h-9"
          >
            {listMutation.isPending ? '···' : t('bottle.listForSale')}
          </Button>
        </div>
      )}

      {(listMutation.isError || unlistMutation.isError) && (
        <div className="mt-2.5 text-sm text-destructive">{t('bottle.errorSale')}</div>
      )}
    </div>
  )
}

function DeleteSection({ bottle, onDelete }: { bottle: Bottle; onDelete?: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const deleteMutation = useMutation({
    mutationFn: () => removeBottle(bottle.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
      onDelete?.()
    },
  })

  return (
    <div className="mb-5">
      <AlertDialog>
        <AlertDialogTrigger asChild>
          <Button
            variant="outline"
            className="w-full border-destructive/45 text-destructive hover:bg-destructive/10"
          >
            {t('bottle.remove')}
          </Button>
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('bottle.remove')}</AlertDialogTitle>
            <AlertDialogDescription>{t('bottle.removeConfirmText')}</AlertDialogDescription>
          </AlertDialogHeader>
          {deleteMutation.isError && (
            <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {t('bottle.removeError')}
            </div>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteMutation.isPending}>
              {t('bottle.removeCancel')}
            </AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              className="bg-destructive text-white hover:bg-destructive/90 dark:bg-destructive dark:hover:bg-destructive/90"
              disabled={deleteMutation.isPending}
              onClick={e => {
                e.preventDefault()
                deleteMutation.mutate()
              }}
            >
              {deleteMutation.isPending ? t('bottle.removing') : t('bottle.removeConfirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

const makeOfferSchema = (t: TFunction) =>
  z.object({
    offeredPrice: z
      .string()
      .min(1, t('offers.priceInvalid'))
      .refine(v => Number(v) > 0, t('offers.priceInvalid')),
    currency: z.string().min(1),
    message: z.string().optional(),
  })

type OfferValues = z.infer<ReturnType<typeof makeOfferSchema>>

function MakeOfferSection({ bottle }: { bottle: Bottle }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const schema = useMemo(() => makeOfferSchema(t), [t])

  const form = useForm<OfferValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      offeredPrice: bottle.askingPrice?.toString() ?? '',
      currency: bottle.currency ?? 'USD',
      message: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (v: OfferValues) =>
      createOffer({
        bottleId: bottle.id,
        offeredPrice: Number(v.offeredPrice),
        currency: v.currency,
        message: v.message?.trim() || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['offers'] })
      setOpen(false)
      form.reset()
    },
    onError: () => form.setError('root', { message: t('offers.errorCreate') }),
  })

  return (
    <div className="mb-5">
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild>
          <Button size="lg" className="h-10 w-full">
            {t('offers.makeOffer')}
          </Button>
        </DialogTrigger>
        <DialogContent className="w-[calc(100%-2rem)] max-w-[440px] sm:max-w-[440px]">
          <DialogHeader>
            <DialogTitle>{t('offers.offerModalTitle')}</DialogTitle>
            <DialogDescription>{bottle.name}</DialogDescription>
          </DialogHeader>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(v => mutation.mutate(v))} className="space-y-4">
              <div className="grid grid-cols-[1fr_110px] gap-3">
                <FormField
                  control={form.control}
                  name="offeredPrice"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('offers.price')}</FormLabel>
                      <FormControl>
                        <Input type="number" min={0} step="0.01" className="h-9" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="currency"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('offers.currency')}</FormLabel>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="h-9 w-full">
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {CURRENCIES.map(c => (
                            <SelectItem key={c} value={c}>
                              {c}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={form.control}
                name="message"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('offers.message')}</FormLabel>
                    <FormControl>
                      <Textarea rows={3} placeholder={t('offers.messagePlaceholder')} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              {form.formState.errors.root && (
                <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  {form.formState.errors.root.message}
                </div>
              )}
              <DialogFooter>
                <DialogClose asChild>
                  <Button type="button" variant="outline">
                    {t('offers.cancel')}
                  </Button>
                </DialogClose>
                <Button type="submit" disabled={mutation.isPending}>
                  {mutation.isPending ? t('offers.submitting') : t('offers.submit')}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  )
}

const CONFIDENCE_VARIANT: Record<PriceConfidence, 'success' | 'secondary' | 'outline'> = {
  High: 'success',
  Medium: 'secondary',
  Low: 'outline',
}

function EstimateSection({ bottleId }: { bottleId: string }) {
  const { t, i18n } = useTranslation()

  const { data: estimate, isLoading } = useQuery({
    queryKey: ['priceEstimate', bottleId],
    queryFn: () => getBottleEstimate(bottleId),
  })

  const formatDate = (iso: string) =>
    new Date(iso).toLocaleDateString(i18n.language === 'bg' ? 'bg-BG' : 'en-GB', { dateStyle: 'medium' })

  return (
    <div className="mb-5 rounded-md border border-primary/15 bg-primary/[0.04] p-4">
      <div className="mb-2 flex items-center justify-between gap-2">
        <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          {t('collectionValue.estimateLabel')}
        </span>
        <Badge variant="outline" className="text-[10px] uppercase">
          {t('collectionValue.indicative')}
        </Badge>
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">{t('collectionValue.loading')}</div>
      ) : !estimate ? (
        <div className="flex items-baseline gap-2">
          <span className="font-heading text-2xl font-semibold text-muted-foreground">
            {t('collectionValue.empty')}
          </span>
          <span className="text-xs text-muted-foreground/80">{t('collectionValue.noEstimate')}</span>
        </div>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-3">
            <span className="font-heading text-2xl font-semibold text-primary">
              {t('collectionValue.range', {
                currency: estimate.currency,
                low: (estimate.lowEstimate ?? estimate.estimatedPrice).toLocaleString(),
                high: (estimate.highEstimate ?? estimate.estimatedPrice).toLocaleString(),
              })}
            </span>
            <Badge variant={CONFIDENCE_VARIANT[estimate.confidence]}>
              {t('collectionValue.confidenceLabel')}: {t(`collectionValue.confidence${estimate.confidence}`)}
            </Badge>
          </div>

          <div className="mt-1.5 text-xs text-muted-foreground">
            {t('collectionValue.sourceLabel')}:{' '}
            {estimate.source === 'ClaudeResearch'
              ? t('collectionValue.sourceResearched')
              : t('collectionValue.sourceCommunity')}
            {' · '}
            {t('collectionValue.asOf', { date: formatDate(estimate.asOf) })}
          </div>

          {estimate.sources.length > 0 && (
            <div className="mt-3 border-t border-primary/10 pt-3">
              <div className="mb-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {t('collectionValue.sources')}
              </div>
              <ul className="flex flex-col gap-1">
                {estimate.sources.map(source => (
                  <li key={source.url}>
                    <a
                      href={source.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="break-all text-sm text-primary underline underline-offset-2 hover:text-primary/80"
                    >
                      {source.title}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </div>
  )
}

function DetailRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs uppercase tracking-wide text-muted-foreground">{label}</span>
      <span className="text-base text-foreground">{value}</span>
    </div>
  )
}

export default function BottleDetailPanel({
  bottle,
  userId,
  currentUserId,
  onClose,
  onDelete,
}: {
  bottle: Bottle
  userId: string
  currentUserId: string
  onClose: () => void
  onDelete?: () => void
}) {
  const { t } = useTranslation()
  const col = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find(i => i.isPrimary) ?? bottle.images[0]
  const galleryImages = bottle.images.filter(i => !i.isPrimary).sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <Dialog open onOpenChange={o => { if (!o) onClose() }}>
      <DialogContent
        showCloseButton={false}
        className="w-[calc(100%-2rem)] max-w-[680px] gap-0 overflow-y-auto p-0 sm:max-w-[680px] max-h-[90vh]"
      >
        <div className="relative h-[280px] w-full overflow-hidden bg-background">
          {primaryImage ? (
            <img
              src={primaryImage.url}
              alt={bottle.name}
              className="size-full object-cover object-[center_top]"
            />
          ) : (
            <div
              className="flex size-full items-center justify-center"
              style={{ background: `radial-gradient(ellipse at 50% 80%, ${col.glow}18 0%, transparent 65%)` }}
            >
              <div className="w-[90px]">
                <BottleSvg category={bottle.category} condition={bottle.condition} />
              </div>
            </div>
          )}

          <div className="absolute inset-0 bg-gradient-to-b from-transparent from-40% to-popover" />

          <DialogClose asChild>
            <Button
              variant="ghost"
              size="icon"
              aria-label={t('bottle.close')}
              className="absolute top-4 right-4 size-8 rounded-full bg-background/70 text-primary hover:bg-background/90 hover:text-primary"
            >
              <X className="size-4" />
            </Button>
          </DialogClose>

          <div className="absolute inset-x-0 bottom-0 px-7 pb-5">
            <div className="mb-1.5 flex flex-wrap items-center gap-2">
              <Badge
                variant="outline"
                style={{ backgroundColor: `${col.glass}22`, borderColor: `${col.glass}66`, color: col.glass }}
              >
                {col.label}
              </Badge>
              <Badge variant="outline">{t(`addBottle.condition${bottle.condition}`)}</Badge>
              {bottle.isLimited && <Badge variant="secondary">{t('bottle.limited')}</Badge>}
              {bottle.isForSale && <Badge variant="success">{t('bottle.forSale')}</Badge>}
            </div>
            <DialogTitle className="font-heading text-2xl font-bold leading-tight text-primary">
              {bottle.name}
            </DialogTitle>
            {bottle.distilleryName ? (
              <DialogDescription className="mt-1 text-base italic text-primary/85">
                {bottle.distilleryName}
              </DialogDescription>
            ) : (
              <DialogDescription className="sr-only">{bottle.name}</DialogDescription>
            )}
          </div>
        </div>

        <div className="px-7 pb-10 pt-6">
          {(bottle.age != null || bottle.abvPercent != null || bottle.volumeMl != null || bottle.vintageYear != null) && (
            <div className="mb-5 grid grid-cols-[repeat(auto-fit,minmax(90px,1fr))] gap-x-6 gap-y-4 rounded-md border border-primary/10 bg-primary/[0.04] p-5">
              {bottle.age != null && <DetailRow label={t('bottle.age')} value={`${bottle.age} yr`} />}
              {bottle.abvPercent != null && <DetailRow label={t('bottle.abv')} value={`${bottle.abvPercent}%`} />}
              {bottle.volumeMl != null && <DetailRow label={t('bottle.volume')} value={`${bottle.volumeMl} ml`} />}
              {bottle.vintageYear != null && <DetailRow label={t('bottle.vintage')} value={bottle.vintageYear} />}
            </div>
          )}

          {(bottle.region || bottle.country) && (
            <div className="mb-5">
              <div className="mb-1.5 text-xs uppercase tracking-wide text-muted-foreground">
                {t('bottle.origin')}
              </div>
              <div className="text-base text-foreground">
                {[bottle.region, bottle.country].filter(Boolean).join(', ')}
              </div>
            </div>
          )}

          {currentUserId && <EstimateSection bottleId={bottle.id} />}

          {bottle.userId === currentUserId ? (
            <>
              <SaleSection bottle={bottle} userId={userId} />
              <DeleteSection bottle={bottle} onDelete={onDelete ?? onClose} />
            </>
          ) : (
            <>
              {bottle.isForSale && bottle.askingPrice != null && (
                <div className="mb-5 flex items-center justify-between rounded-md border border-success/30 bg-success/10 px-5 py-3.5">
                  <span className="text-xs uppercase tracking-wide text-success">
                    {t('bottle.askingPrice')}
                  </span>
                  <span className="font-heading text-xl font-semibold text-success">
                    {bottle.currency ?? 'USD'} {bottle.askingPrice.toLocaleString()}
                  </span>
                </div>
              )}
              {currentUserId && <MakeOfferSection bottle={bottle} />}
            </>
          )}

          {bottle.description && (
            <div className="mb-5">
              <div className="mb-2 text-xs uppercase tracking-wide text-muted-foreground">
                {t('bottle.notes')}
              </div>
              <p className="text-base italic leading-relaxed text-primary">{bottle.description}</p>
            </div>
          )}

          {galleryImages.length > 0 && (
            <div>
              <div className="mb-2 text-xs uppercase tracking-wide text-muted-foreground">
                {t('bottle.gallery')}
              </div>
              <div className="flex gap-2 overflow-x-auto [scrollbar-width:none]">
                {galleryImages.map(img => (
                  <img
                    key={img.id}
                    src={img.url}
                    alt=""
                    className="size-20 shrink-0 rounded border border-primary/15 object-cover"
                  />
                ))}
              </div>
            </div>
          )}

          <LikesSection bottle={bottle} userId={userId} />

          <ReviewsSection bottle={bottle} />

          <CommentsSection bottle={bottle} currentUserId={currentUserId} />
        </div>
      </DialogContent>
    </Dialog>
  )
}
