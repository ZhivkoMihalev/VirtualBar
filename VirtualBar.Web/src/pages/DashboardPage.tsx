import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ImagePlus, Loader2, Check } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import NavBar from '../components/NavBar'
import {
  getBottlesByUser,
  addBottle,
  uploadBottleImage,
  linkBottleImage,
  lookupBarcode,
} from '../api/bottlesApi'
import type { Bottle, SpiritCategory, AddBottlePayload } from '../types'
import { CATEGORY_COLORS, BottleSvg, VirtualBarScene } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import DistillerySelect from '../components/DistillerySelect'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Card, CardContent } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { Switch } from '@/components/ui/switch'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
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
const CONDITIONS = ['Sealed', 'Opened', 'Empty'] as const

const TOGGLE_ON = 'data-[state=on]:border-primary data-[state=on]:bg-primary/10 data-[state=on]:text-primary'

const makeSchema = (t: TFn) =>
  z.object({
    name: z.string().trim().min(1, t('addBottle.nameRequired')),
    category: z.enum(['Whisky', 'Rum', 'Cognac', 'Vodka', 'Gin', 'Tequila', 'Brandy', 'Other']),
    condition: z.enum(['Sealed', 'Opened', 'Empty']),
    distilleryId: z.string().nullable(),
    age: z.string(),
    abv: z.string(),
    volume: z.string(),
    isLimited: z.boolean(),
    description: z.string(),
  })

type Values = z.infer<ReturnType<typeof makeSchema>>

function StatItem({ value, label }: { value: number; label: string }) {
  return (
    <div>
      <div className="text-2xl font-semibold text-primary">{value}</div>
      <div className="text-xs uppercase tracking-wider text-muted-foreground">{label}</div>
    </div>
  )
}

function AddBottlePanel({ onSuccess }: { onSuccess: () => void }) {
  const { t } = useTranslation()
  const schema = useMemo(() => makeSchema(t), [t])

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '',
      category: 'Whisky',
      condition: 'Sealed',
      distilleryId: null,
      age: '',
      abv: '',
      volume: '',
      isLimited: false,
      description: '',
    },
  })

  const category = useWatch({ control: form.control, name: 'category' })
  const condition = useWatch({ control: form.control, name: 'condition' })

  const [imageFile, setImageFile] = useState<File | null>(null)
  const [imagePreview, setImagePreview] = useState<string | null>(null)
  const [barcodeImageUrl, setBarcodeImageUrl] = useState<string | null>(null)
  const [dropHover, setDropHover] = useState(false)
  const [barcode, setBarcode] = useState('')
  const [barcodeLoading, setBarcodeLoading] = useState(false)
  const [barcodeStatus, setBarcodeStatus] = useState<'idle' | 'found' | 'error'>('idle')

  const handleImageChange = (file: File | null) => {
    if (!file) return
    setImageFile(file)
    setBarcodeImageUrl(null)
    setImagePreview(URL.createObjectURL(file))
  }

  const handleBarcodeSearch = async () => {
    const code = barcode.trim()
    if (!code) return
    setBarcodeLoading(true)
    setBarcodeStatus('idle')
    try {
      const product = await lookupBarcode(code)
      if (product.name) form.setValue('name', product.name)
      if (product.volumeMl) form.setValue('volume', String(product.volumeMl))
      if (product.abvPercent) form.setValue('abv', String(product.abvPercent))
      if (product.imageUrl) {
        setImagePreview(product.imageUrl)
        setImageFile(null)
        setBarcodeImageUrl(product.imageUrl)
      }
      setBarcodeStatus('found')
    } catch {
      setBarcodeStatus('error')
    } finally {
      setBarcodeLoading(false)
    }
  }

  const mutation = useMutation({
    mutationFn: async (payload: AddBottlePayload) => {
      const bottle = await addBottle(payload)
      if (imageFile) {
        await uploadBottleImage(bottle.id, imageFile)
      } else if (barcodeImageUrl) {
        await linkBottleImage(bottle.id, barcodeImageUrl)
      }
      return bottle
    },
    onSuccess: () => onSuccess(),
    onError: () => form.setError('root', { message: t('addBottle.error') }),
  })

  const onSubmit = (v: Values) => {
    const payload: AddBottlePayload = {
      name: v.name.trim(),
      distilleryId: v.distilleryId,
      category: v.category,
      condition: v.condition,
      isLimited: v.isLimited,
      age: v.age ? Number(v.age) : undefined,
      abvPercent: v.abv ? Number(v.abv) : undefined,
      volumeMl: v.volume ? Number(v.volume) : undefined,
      description: v.description.trim() || undefined,
    }
    mutation.mutate(payload)
  }

  return (
    <div className="space-y-5 px-6 pb-8">
      <div>
        <div
          onDragOver={e => { e.preventDefault(); setDropHover(true) }}
          onDragLeave={() => setDropHover(false)}
          onDrop={e => {
            e.preventDefault()
            setDropHover(false)
            const file = e.dataTransfer.files[0]
            if (file && file.type.startsWith('image/')) handleImageChange(file)
          }}
          onClick={() => document.getElementById('bottle-image-input')?.click()}
          className={cn(
            'group relative flex h-44 cursor-pointer items-center justify-center overflow-hidden rounded-md border-2 border-dashed transition-colors',
            dropHover ? 'border-primary/70 bg-primary/5' : 'border-primary/25 bg-primary/[0.02]',
          )}
        >
          {imagePreview ? (
            <>
              <img src={imagePreview} alt="preview" className="size-full object-cover" />
              <div className="absolute inset-0 flex items-center justify-center bg-black/45 opacity-0 transition-opacity group-hover:opacity-100">
                <span className="text-xs font-medium uppercase tracking-wide text-primary">
                  {t('addBottle.changePhoto')}
                </span>
              </div>
            </>
          ) : (
            <div className="pointer-events-none text-center">
              <ImagePlus className="mx-auto mb-2 size-8 text-primary/50" />
              <div className="text-xs font-medium uppercase tracking-wide text-primary/70">
                {t('addBottle.uploadPhoto')}
              </div>
              <div className="text-xs text-muted-foreground">{t('addBottle.clickOrDrag')}</div>
            </div>
          )}
        </div>

        <input
          id="bottle-image-input"
          type="file"
          accept="image/jpeg,image/png,image/webp,image/gif"
          className="hidden"
          onChange={e => handleImageChange(e.target.files?.[0] ?? null)}
        />

        <div className="mt-4 flex justify-center">
          <div className="w-[50px]">
            <BottleSvg category={category} condition={condition} />
          </div>
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="barcode-input">{t('addBottle.barcodeLookup')}</Label>
        <div className="flex gap-2">
          <Input
            id="barcode-input"
            value={barcode}
            onChange={e => setBarcode(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter') {
                e.preventDefault()
                handleBarcodeSearch()
              }
            }}
            placeholder={t('addBottle.barcodePlaceholder')}
            className="h-9 flex-1"
          />
          <Button
            type="button"
            variant="secondary"
            onClick={handleBarcodeSearch}
            disabled={!barcode.trim() || barcodeLoading}
            className="h-9 shrink-0"
          >
            {barcodeLoading ? <Loader2 className="size-3.5 animate-spin" /> : t('addBottle.barcodeFind')}
          </Button>
        </div>
        {barcodeStatus === 'found' && (
          <div className="flex items-center gap-1.5 rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-success">
            <Check className="size-3.5" />
            {t('addBottle.barcodeSuccess')}
          </div>
        )}
        {barcodeStatus === 'error' && (
          <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {t('addBottle.barcodeNotFound')}
          </div>
        )}
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <FormField
            control={form.control}
            name="category"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('addBottle.category')}</FormLabel>
                <FormControl>
                  <ToggleGroup
                    type="single"
                    variant="outline"
                    value={field.value}
                    onValueChange={v => {
                      if (v) {
                        field.onChange(v)
                        form.setValue('distilleryId', null)
                      }
                    }}
                    className="grid w-full grid-cols-4 gap-2"
                  >
                    {CATEGORIES.map(cat => (
                      <ToggleGroupItem
                        key={cat}
                        value={cat}
                        className={cn('h-auto flex-col gap-1 py-2', TOGGLE_ON)}
                      >
                        <span className="size-2.5 rounded-full" style={{ background: CATEGORY_COLORS[cat].glass }} />
                        <span className="text-[11px]">{CATEGORY_COLORS[cat].label}</span>
                      </ToggleGroupItem>
                    ))}
                  </ToggleGroup>
                </FormControl>
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('addBottle.name')}</FormLabel>
                <FormControl>
                  <Input className="h-9" placeholder={t('addBottle.namePlaceholder')} {...field} />
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
                <FormLabel>{t('addBottle.distillery')}</FormLabel>
                <DistillerySelect
                  value={field.value}
                  onChange={field.onChange}
                  category={category || undefined}
                  placeholder={t('addBottle.distilleryPlaceholder')}
                />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-3 gap-3">
            <FormField
              control={form.control}
              name="age"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('addBottle.age')}</FormLabel>
                  <FormControl>
                    <Input type="number" min={0} max={100} className="h-9" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="abv"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('addBottle.abv')}</FormLabel>
                  <FormControl>
                    <Input type="number" min={0} max={100} step="0.1" className="h-9" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="volume"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('addBottle.volume')}</FormLabel>
                  <FormControl>
                    <Input type="number" min={0} className="h-9" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="condition"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('addBottle.condition')}</FormLabel>
                <FormControl>
                  <ToggleGroup
                    type="single"
                    variant="outline"
                    value={field.value}
                    onValueChange={v => { if (v) field.onChange(v) }}
                    className="grid w-full grid-cols-3 gap-2"
                  >
                    {CONDITIONS.map(cond => (
                      <ToggleGroupItem key={cond} value={cond} className={TOGGLE_ON}>
                        {t(`addBottle.condition${cond}`)}
                      </ToggleGroupItem>
                    ))}
                  </ToggleGroup>
                </FormControl>
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="isLimited"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-md border border-input p-3">
                <FormLabel className="cursor-pointer">{t('addBottle.limited')}</FormLabel>
                <FormControl>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="description"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('addBottle.description')}</FormLabel>
                <FormControl>
                  <Textarea rows={3} placeholder={t('addBottle.descriptionPlaceholder')} {...field} />
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

          <Button type="submit" size="lg" className="h-10 w-full" disabled={mutation.isPending}>
            {mutation.isPending ? t('addBottle.submitting') : t('addBottle.submit')}
          </Button>
        </form>
      </Form>
    </div>
  )
}

export default function DashboardPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)
  const [activeCategory, setActiveCategory] = useState<SpiritCategory | null>(null)

  const { data: bottles = [], isLoading } = useQuery({
    queryKey: ['bottles', user?.id],
    queryFn: () => getBottlesByUser(user!.id),
    enabled: !!user?.id,
  })

  const displayedBottles = useMemo(
    () => (activeCategory ? bottles.filter(b => b.category === activeCategory) : bottles),
    [bottles, activeCategory],
  )

  const categoryCounts = useMemo(
    () =>
      CATEGORIES.reduce((acc, cat) => {
        acc[cat] = bottles.filter(b => b.category === cat).length
        return acc
      }, {} as Record<SpiritCategory, number>),
    [bottles],
  )

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[1200px] px-6 py-10 sm:px-10">
        <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
          <div>
            <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">
              {t('dashboard.yourCollection')}
            </div>
            <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">
              {t('dashboard.virtualBar')}
            </h1>
            <div className="mt-1.5 text-lg italic text-primary/90">
              {bottles.length === 0
                ? t('dashboard.collectionAwaits')
                : t('dashboard.bottlesInCollection', { count: bottles.length })}
            </div>
          </div>

          <Button onClick={() => setAddOpen(true)}>{t('dashboard.addBottle')}</Button>
        </div>

        {bottles.length > 0 && (
          <Card className="mb-8">
            <CardContent className="flex flex-wrap items-center gap-6 px-6 py-4">
              <StatItem value={bottles.length} label={t('dashboard.totalBottles')} />
              <Separator orientation="vertical" className="h-10" />
              <StatItem
                value={bottles.filter(b => b.condition === 'Sealed').length}
                label={t('dashboard.sealed')}
              />
              <Separator orientation="vertical" className="h-10" />
              <StatItem value={bottles.filter(b => b.isForSale).length} label={t('dashboard.forSale')} />
              <Separator orientation="vertical" className="h-10" />
              <StatItem value={bottles.filter(b => b.isLimited).length} label={t('dashboard.limited')} />
            </CardContent>
          </Card>
        )}

        {isLoading && (
          <div className="animate-[shimmer_1.6s_ease-in-out_infinite] py-20 text-center text-xs uppercase tracking-widest text-primary">
            {t('dashboard.loading')}
          </div>
        )}

        {!isLoading && bottles.length > 0 && (
          <VirtualBarScene
            bottles={displayedBottles}
            onAdd={() => setAddOpen(true)}
            onSelect={setSelectedBottle}
          />
        )}

        {bottles.length > 0 && (
          <ToggleGroup
            type="single"
            variant="outline"
            value={activeCategory ?? 'all'}
            onValueChange={v => setActiveCategory(!v || v === 'all' ? null : (v as SpiritCategory))}
            className="mt-7 flex w-full flex-wrap justify-start gap-2"
          >
            <ToggleGroupItem value="all" className={TOGGLE_ON}>
              {t('marketplace.allCategories')}
              <span className="ml-1.5 opacity-70">{bottles.length}</span>
            </ToggleGroupItem>
            {CATEGORIES.filter(c => categoryCounts[c] > 0).map(cat => (
              <ToggleGroupItem key={cat} value={cat} className={TOGGLE_ON}>
                {CATEGORY_COLORS[cat].label}
                <span className="ml-1.5 opacity-70">{categoryCounts[cat]}</span>
              </ToggleGroupItem>
            ))}
          </ToggleGroup>
        )}

        {bottles.length === 0 && !isLoading && (
          <div className="animate-[fadeInUp_0.6s_ease-out] py-16 text-center">
            <div className="mb-4 text-xs uppercase tracking-widest text-primary">
              {t('dashboard.emptyTitle')}
            </div>
            <p className="mx-auto mb-7 max-w-sm text-xl italic text-primary/90">
              {t('dashboard.emptySubtitle')}
            </p>
            <Button variant="outline" onClick={() => setAddOpen(true)}>
              {t('dashboard.emptyButton')}
            </Button>
          </div>
        )}
      </main>

      <Sheet open={addOpen} onOpenChange={setAddOpen}>
        <SheetContent
          side="right"
          className="overflow-y-auto data-[side=right]:w-full data-[side=right]:sm:max-w-[480px]"
        >
          <SheetHeader>
            <SheetTitle>{t('addBottle.title')}</SheetTitle>
            <SheetDescription className="sr-only">{t('addBottle.title')}</SheetDescription>
          </SheetHeader>
          <AddBottlePanel
            onSuccess={() => {
              queryClient.invalidateQueries({ queryKey: ['bottles', user?.id] })
              setAddOpen(false)
            }}
          />
        </SheetContent>
      </Sheet>

      {selectedBottle && user && (
        <BottleDetailPanel
          bottle={bottles.find(b => b.id === selectedBottle.id) ?? selectedBottle}
          userId={user.id}
          currentUserId={user.id}
          onClose={() => setSelectedBottle(null)}
          onDelete={() => {
            queryClient.invalidateQueries({ queryKey: ['bottles', user.id] })
            setSelectedBottle(null)
          }}
        />
      )}
    </div>
  )
}
