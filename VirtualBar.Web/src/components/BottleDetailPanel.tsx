import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Heart, X } from 'lucide-react'
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
import type { Bottle } from '../types'
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

          <CommentsSection bottle={bottle} currentUserId={currentUserId} />
        </div>
      </DialogContent>
    </Dialog>
  )
}
