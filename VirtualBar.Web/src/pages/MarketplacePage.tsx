import { useEffect, useMemo, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Search, Gem, Upload, Loader2, X } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { useChat } from '../contexts/ChatContext'
import { getMarketplace } from '../api/bottlesApi'
import { sendMessage } from '../api/messagesApi'
import {
  getAllWishListItems,
  addWishListItem,
  removeWishListItem,
  uploadWishListImage,
} from '../api/wishListApi'
import type { AddWishListItemRequest } from '../api/wishListApi'
import type { Bottle, SpiritCategory, PublicWishListItem } from '../types'
import { CATEGORY_COLORS, BottleSvg } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import DistillerySelect from '../components/DistillerySelect'
import NavBar from '../components/NavBar'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Switch } from '@/components/ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
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
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import type { TFn } from '@/lib/validation'

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]
const ANY = 'any'

type SortOption = 'price_asc' | 'price_desc' | 'newest'
type MarketTab = 'sale' | 'search'

const conditionColor: Record<string, string> = {
  Sealed: '#4A9A6A',
  Opened: '#C8820A',
  Empty: '#7A6040',
}

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(handle)
  }, [value, delay])

  return debounced
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function CategoryPill({
  label,
  active,
  color,
  onClick,
}: {
  label: string
  active: boolean
  color?: string
  onClick: () => void
}) {
  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={onClick}
      className={cn(
        'shrink-0 rounded-full',
        active && !color && 'border-primary bg-primary/10 text-primary',
        !active && 'border-border text-muted-foreground',
      )}
      style={active && color ? { background: `${color}26`, borderColor: color, color } : undefined}
    >
      {label}
    </Button>
  )
}

function MarketplaceCard({ bottle, onView }: { bottle: Bottle; onView: () => void }) {
  const { t } = useTranslation()
  const cat = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find(i => i.isPrimary) ?? bottle.images[0]

  return (
    <Card className="group flex flex-col gap-0 overflow-hidden p-0 transition-all hover:-translate-y-0.5 hover:ring-primary/30">
      <div
        className="flex h-40 items-center justify-center overflow-hidden"
        style={{ background: `radial-gradient(ellipse at 50% 40%, ${cat.glow}1A, var(--background))` }}
      >
        {primaryImage ? (
          <img src={primaryImage.url} alt={bottle.name} className="size-full object-cover" />
        ) : (
          <div className="w-[50px]">
            <BottleSvg category={bottle.category} condition={bottle.condition} />
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
          <Badge
            variant="outline"
            style={{
              backgroundColor: `${conditionColor[bottle.condition]}1A`,
              borderColor: `${conditionColor[bottle.condition]}55`,
              color: conditionColor[bottle.condition],
            }}
          >
            {t(`addBottle.condition${bottle.condition}`)}
          </Badge>
          {bottle.isLimited && <Gem className="size-3 text-primary" />}
          {bottle.reviewsCount > 0 && bottle.averageScore != null && (
            <Badge variant="outline" className="border-primary/40 text-primary">
              ★ {Math.round(bottle.averageScore)}
            </Badge>
          )}
        </div>

        <div className="font-heading text-base font-semibold text-foreground">{bottle.name}</div>
        {bottle.distilleryName && (
          <div className="text-sm italic text-muted-foreground">{bottle.distilleryName}</div>
        )}

        <div className="mt-1.5 flex flex-wrap gap-3 text-xs text-muted-foreground">
          {bottle.age != null && <span>{bottle.age} yr</span>}
          {bottle.abvPercent != null && <span>{bottle.abvPercent}% ABV</span>}
          {bottle.volumeMl != null && <span>{bottle.volumeMl} ml</span>}
        </div>

        <div className="mt-auto pt-4">
          <div className="mb-2 font-heading text-lg font-semibold text-primary">
            {bottle.askingPrice != null
              ? `${bottle.currency ?? ''} ${bottle.askingPrice.toLocaleString()}`.trim()
              : t('marketplace.priceOnRequest')}
          </div>

          <Link
            to={`/bar/${bottle.userId}`}
            className="mb-3 inline-block text-sm italic text-muted-foreground hover:text-foreground"
          >
            {t('marketplace.by', { name: bottle.userDisplayName })}
          </Link>

          <Button variant="outline" size="sm" className="w-full" onClick={onView}>
            {t('marketplace.viewBottle')}
          </Button>
        </div>
      </div>
    </Card>
  )
}

function ContactModal({ item }: { item: PublicWishListItem }) {
  const { t } = useTranslation()
  const { openChat } = useChat()
  const [open, setOpen] = useState(false)
  const [content, setContent] = useState(() => t('wishList.contactDefaultMessage'))

  const sendMutation = useMutation({
    mutationFn: () => sendMessage(item.userId, content.trim()),
    onSuccess: () => {
      openChat(item.userId)
      setOpen(false)
    },
  })

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!content.trim()) return
    sendMutation.mutate()
  }

  return (
    <Dialog
      open={open}
      onOpenChange={o => {
        setOpen(o)
        if (o) setContent(t('wishList.contactDefaultMessage'))
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm" className="shrink-0">
          {t('wishList.contactBtn')}
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('wishList.contactTitle')}</DialogTitle>
          <DialogDescription className="sr-only">{t('wishList.contactTitle')}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <Textarea value={content} onChange={e => setContent(e.target.value)} rows={5} />
          {sendMutation.isError && (
            <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {t('wishList.contactError')}
            </div>
          )}
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">
                {t('wishList.contactCancel')}
              </Button>
            </DialogClose>
            <Button type="submit" disabled={sendMutation.isPending || !content.trim()}>
              {sendMutation.isPending ? t('wishList.contactSending') : t('wishList.contactSend')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function WishCard({
  item,
  isOwn,
  canContact,
}: {
  item: PublicWishListItem
  isOwn: boolean
  canContact: boolean
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const removeMutation = useMutation({
    mutationFn: () => removeWishListItem(item.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wishlist', 'all'] }),
  })

  return (
    <Card className="flex flex-row items-center justify-between gap-4 p-4">
      <div className="min-w-0 flex-1">
        <div className="mb-1.5 text-xs uppercase tracking-wide text-muted-foreground">
          {item.userDisplayName}
        </div>
        {item.bottleName && (
          <div className="font-heading text-lg font-semibold text-foreground">{item.bottleName}</div>
        )}
        <div className="mt-1.5 flex flex-wrap items-center gap-2">
          {item.distilleryName && <Badge variant="outline">{item.distilleryName}</Badge>}
          {item.category && <Badge variant="secondary">{CATEGORY_COLORS[item.category].label}</Badge>}
        </div>
        <div className="mt-2 text-xs text-muted-foreground">{formatDate(item.createdAt)}</div>
      </div>

      {item.imageUrl && (
        <img
          src={item.imageUrl}
          alt={item.bottleName ?? ''}
          className="size-16 shrink-0 rounded-md border border-border object-cover"
        />
      )}

      {isOwn && (
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button
              variant="outline"
              size="sm"
              className="shrink-0 border-destructive/40 text-destructive hover:bg-destructive/10"
            >
              {t('wishList.remove')}
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('wishList.remove')}</AlertDialogTitle>
              <AlertDialogDescription className="sr-only">{t('wishList.remove')}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={removeMutation.isPending}>
                {t('wishList.contactCancel')}
              </AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                className="bg-destructive text-white hover:bg-destructive/90 dark:bg-destructive dark:hover:bg-destructive/90"
                disabled={removeMutation.isPending}
                onClick={e => {
                  e.preventDefault()
                  removeMutation.mutate()
                }}
              >
                {t('wishList.removeConfirm')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}

      {!isOwn && canContact && <ContactModal item={item} />}
    </Card>
  )
}

const makePublishSchema = (t: TFn) =>
  z
    .object({
      bottleName: z.string(),
      distilleryId: z.string().nullable(),
      category: z.string(),
      imageUrl: z.string(),
    })
    .refine(v => Boolean(v.distilleryId) || v.category !== ANY, {
      path: ['category'],
      message: t('wishList.atLeastOne'),
    })

type PublishValues = z.infer<ReturnType<typeof makePublishSchema>>

function PublishDialogBody({ onPosted }: { onPosted: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => makePublishSchema(t), [t])

  const form = useForm<PublishValues>({
    resolver: zodResolver(schema),
    defaultValues: { bottleName: '', distilleryId: null, category: ANY, imageUrl: '' },
  })

  const [uploadMode, setUploadMode] = useState(false)
  const categoryValue = useWatch({ control: form.control, name: 'category' })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadWishListImage(file),
    onSuccess: result => form.setValue('imageUrl', result.url, { shouldDirty: true }),
  })

  const addMutation = useMutation({
    mutationFn: (payload: AddWishListItemRequest) => addWishListItem(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['wishlist', 'all'] })
      onPosted()
    },
  })

  function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) uploadMutation.mutate(file)
    e.target.value = ''
  }

  const onSubmit = (v: PublishValues) => {
    addMutation.mutate({
      bottleName: v.bottleName.trim() || undefined,
      distilleryId: v.distilleryId,
      category: v.category === ANY ? undefined : v.category,
      imageUrl: v.imageUrl || undefined,
    })
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid gap-3.5 sm:grid-cols-3">
          <FormField
            control={form.control}
            name="bottleName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('wishList.bottleName')}</FormLabel>
                <FormControl>
                  <Input className="h-9" placeholder={t('wishList.bottleNamePlaceholder')} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="distilleryId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('wishList.distillery')}</FormLabel>
                <DistillerySelect
                  value={field.value}
                  onChange={field.onChange}
                  category={categoryValue === ANY ? undefined : categoryValue}
                  placeholder={t('wishList.distilleryPlaceholder')}
                />
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="category"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('wishList.category')}</FormLabel>
                <Select
                  value={field.value}
                  onValueChange={v => {
                    field.onChange(v)
                    form.setValue('distilleryId', null)
                  }}
                >
                  <FormControl>
                    <SelectTrigger className="h-9 w-full">
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    <SelectItem value={ANY}>{t('wishList.categoryAny')}</SelectItem>
                    {CATEGORIES.map(cat => (
                      <SelectItem key={cat} value={cat}>
                        {CATEGORY_COLORS[cat].label}
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
          name="imageUrl"
          render={({ field }) => (
            <FormItem>
              <div className="flex items-center justify-between gap-2">
                <FormLabel>{uploadMode ? t('wishList.uploadImage') : t('wishList.imageUrl')}</FormLabel>
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span>{t('wishList.imageTabUrl')}</span>
                  <Switch
                    checked={uploadMode}
                    onCheckedChange={setUploadMode}
                    aria-label={t('wishList.toggleImageSource')}
                  />
                  <span>{t('wishList.imageTabUpload')}</span>
                </div>
              </div>

              {uploadMode ? (
                <div className="space-y-2">
                  <Button type="button" variant="outline" size="sm" className="h-9" asChild>
                    <label className={uploadMutation.isPending ? 'pointer-events-none opacity-60' : undefined}>
                      {uploadMutation.isPending ? (
                        <Loader2 className="size-3.5 animate-spin" />
                      ) : (
                        <Upload className="size-3.5" />
                      )}
                      {t('wishList.chooseFile')}
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        className="hidden"
                        onChange={handleFileChange}
                        disabled={uploadMutation.isPending}
                      />
                    </label>
                  </Button>
                  {uploadMutation.isError && (
                    <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                      {t('wishList.uploadError')}
                    </div>
                  )}
                </div>
              ) : (
                <FormControl>
                  <Input className="h-9" placeholder={t('wishList.imageUrlPlaceholder')} {...field} />
                </FormControl>
              )}

              {field.value && (
                <div className="relative inline-block">
                  <img
                    src={field.value}
                    alt=""
                    className="size-20 rounded-md border border-border object-cover"
                  />
                  <Button
                    type="button"
                    variant="secondary"
                    size="icon-xs"
                    onClick={() => field.onChange('')}
                    className="absolute -right-2 -top-2"
                    aria-label={t('wishList.removeImage')}
                  >
                    <X className="size-3.5" />
                  </Button>
                </div>
              )}
            </FormItem>
          )}
        />

        {addMutation.isError && (
          <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {t('wishList.addFailed')}
          </div>
        )}

        <Button type="submit" size="lg" className="h-10 w-full" disabled={addMutation.isPending}>
          {addMutation.isPending ? t('wishList.adding') : t('wishList.addBtn')}
        </Button>
      </form>
    </Form>
  )
}

function SearchTab() {
  const { t } = useTranslation()
  const { user, isAuthenticated } = useAuth()
  const [modalOpen, setModalOpen] = useState(false)

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['wishlist', 'all'],
    queryFn: getAllWishListItems,
  })

  return (
    <div className="space-y-6">
      {isAuthenticated && (
        <Dialog open={modalOpen} onOpenChange={setModalOpen}>
          <DialogTrigger asChild>
            <Button>{t('wishList.publishSearch')}</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-2xl">
            <DialogHeader>
              <DialogTitle>{t('wishList.publishSearchTitle')}</DialogTitle>
              <DialogDescription className="sr-only">{t('wishList.subtitle')}</DialogDescription>
            </DialogHeader>
            <PublishDialogBody onPosted={() => setModalOpen(false)} />
          </DialogContent>
        </Dialog>
      )}

      {isLoading && <div className="py-16 text-center text-sm tracking-widest text-muted-foreground">···</div>}

      {!isLoading && items.length === 0 && (
        <div className="py-16 text-center text-xl italic text-muted-foreground">
          {t('marketplace.searchEmpty')}
        </div>
      )}

      {!isLoading && items.length > 0 && (
        <div className="flex flex-col gap-3.5">
          {items.map(item => (
            <WishCard
              key={item.id}
              item={item}
              isOwn={item.userId === user?.id}
              canContact={isAuthenticated}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function SaleTab({ onView }: { onView: (bottle: Bottle) => void }) {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string>('')
  const [sort, setSort] = useState<SortOption>('newest')
  const debouncedSearch = useDebounced(search, 300)

  const { data: bottles = [], isLoading } = useQuery({
    queryKey: ['marketplace', debouncedSearch, category, sort],
    queryFn: () =>
      getMarketplace({
        search: debouncedSearch || undefined,
        category: category || undefined,
        sort,
      }),
  })

  return (
    <div className="space-y-8">
      <div className="flex flex-wrap items-center gap-4">
        <div className="relative w-full max-w-xs">
          <Search className="absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder={t('marketplace.searchPlaceholder')}
            className="h-9 pl-8"
          />
        </div>

        <div className="flex flex-1 flex-wrap gap-2">
          <CategoryPill
            label={t('marketplace.allCategories')}
            active={category === ''}
            onClick={() => setCategory('')}
          />
          {CATEGORIES.map(cat => (
            <CategoryPill
              key={cat}
              label={CATEGORY_COLORS[cat].label}
              active={category === cat}
              color={CATEGORY_COLORS[cat].glass}
              onClick={() => setCategory(category === cat ? '' : cat)}
            />
          ))}
        </div>

        <Select value={sort} onValueChange={v => setSort(v as SortOption)}>
          <SelectTrigger className="h-9 w-auto min-w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="newest">{t('marketplace.sortNewest')}</SelectItem>
            <SelectItem value="price_asc">{t('marketplace.sortPriceAsc')}</SelectItem>
            <SelectItem value="price_desc">{t('marketplace.sortPriceDesc')}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {isLoading && (
        <div className="animate-[shimmer_1.6s_ease-in-out_infinite] py-20 text-center text-xs uppercase tracking-widest text-primary">
          {t('marketplace.loading')}
        </div>
      )}

      {!isLoading && bottles.length === 0 && (
        <div className="py-16 text-center text-xl italic text-muted-foreground">
          {t('marketplace.empty')}
        </div>
      )}

      {!isLoading && bottles.length > 0 && (
        <div className="grid grid-cols-[repeat(auto-fill,minmax(260px,1fr))] gap-5">
          {bottles.map(bottle => (
            <MarketplaceCard key={bottle.id} bottle={bottle} onView={() => onView(bottle)} />
          ))}
        </div>
      )}
    </div>
  )
}

export default function MarketplacePage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [tab, setTab] = useState<MarketTab>('sale')
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-6xl px-6 py-10">
        <div className="mb-6">
          <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">
            {t('marketplace.label')}
          </div>
          <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">
            {tab === 'sale' ? t('marketplace.title') : t('marketplace.titleSearch')}
          </h1>
        </div>

        <Tabs value={tab} onValueChange={v => setTab(v as MarketTab)} className="gap-8">
          <TabsList variant="line">
            <TabsTrigger value="sale">{t('marketplace.tabSale')}</TabsTrigger>
            <TabsTrigger value="search">{t('marketplace.tabSearch')}</TabsTrigger>
          </TabsList>
          <TabsContent value="sale">
            <SaleTab onView={setSelectedBottle} />
          </TabsContent>
          <TabsContent value="search">
            <SearchTab />
          </TabsContent>
        </Tabs>
      </main>

      {selectedBottle && (
        <BottleDetailPanel
          bottle={selectedBottle}
          userId={selectedBottle.userId}
          currentUserId={user?.id ?? ''}
          onClose={() => setSelectedBottle(null)}
        />
      )}
    </div>
  )
}
