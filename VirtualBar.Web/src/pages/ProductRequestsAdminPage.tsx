import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Check, X } from 'lucide-react'
import NavBar from '../components/NavBar'
import DistillerySelect from '../components/DistillerySelect'
import ProductSelect from '../components/ProductSelect'
import {
  getProductRequests,
  approveProductRequest,
  rejectProductRequest,
} from '../api/productRequestsApi'
import type {
  ApproveProductRequestPayload,
  Product,
  ProductRequest,
  ProductRequestStatus,
  SpiritCategory,
} from '../types'
import { CATEGORY_COLORS } from '../components/BarShelf'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
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
import type { TFn } from '@/lib/validation'

const STATUSES: ProductRequestStatus[] = ['Pending', 'Approved', 'Rejected']

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]

const STATUS_VARIANTS: Record<ProductRequestStatus, 'warning' | 'success' | 'destructive'> = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'destructive',
}

const makeApproveSchema = (t: TFn) =>
  z.object({
    name: z.string().trim().min(1, t('addBottle.nameRequired')),
    brand: z.string(),
    distilleryId: z.string().nullable(),
    category: z.enum(['Whisky', 'Rum', 'Cognac', 'Vodka', 'Gin', 'Tequila', 'Brandy', 'Other']),
    age: z.string(),
    abv: z.string(),
    volume: z.string(),
    barcode: z.string(),
    country: z.string(),
    region: z.string(),
    imageUrl: z.string(),
    description: z.string(),
    adminNote: z.string(),
  })

type ApproveValues = z.infer<ReturnType<typeof makeApproveSchema>>

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function toText(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

function toNumber(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

function StateMessage({ text, error }: { text: string; error?: boolean }) {
  return (
    <div className={cn('py-16 text-center text-sm italic', error ? 'text-destructive' : 'text-muted-foreground')}>
      {text}
    </div>
  )
}

function requestMeta(request: ProductRequest, t: TFunction): string {
  return [
    CATEGORY_COLORS[request.category]?.label ?? request.category,
    request.distilleryName ?? request.brand ?? null,
    request.age != null ? t('products.ageSuffix', { age: request.age }) : null,
    request.abvPercent != null ? `${request.abvPercent}% ABV` : null,
    request.volumeMl != null ? t('products.volumeSuffix', { volume: request.volumeMl }) : null,
    request.barcode ?? null,
  ]
    .filter(Boolean)
    .join(' · ')
}

function ApproveDialog({
  request,
  open,
  onOpenChange,
}: {
  request: ProductRequest
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => makeApproveSchema(t), [t])

  const [linkMode, setLinkMode] = useState(false)
  const [linkedProduct, setLinkedProduct] = useState<Product | null>(null)
  const [useSourceImage, setUseSourceImage] = useState(true)

  const form = useForm<ApproveValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: request.name,
      brand: request.brand ?? '',
      distilleryId: request.distilleryId ?? null,
      category: request.category,
      age: request.age != null ? String(request.age) : '',
      abv: request.abvPercent != null ? String(request.abvPercent) : '',
      volume: request.volumeMl != null ? String(request.volumeMl) : '',
      barcode: request.barcode ?? '',
      country: request.country ?? '',
      region: request.region ?? '',
      imageUrl: '',
      description: '',
      adminNote: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (payload: ApproveProductRequestPayload) => approveProductRequest(request.id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['productRequests'] })
      // The approval created (or enriched) a catalog product — drop cached search results.
      queryClient.invalidateQueries({ queryKey: ['products'] })
      toast.success(t('productRequests.approveSuccess'))
      onOpenChange(false)
    },
    onError: () => toast.error(t('productRequests.error')),
  })

  const onSubmit = (values: ApproveValues) => {
    if (linkMode) {
      if (!linkedProduct) return
      mutation.mutate({
        existingProductId: linkedProduct.id,
        adminNote: toText(values.adminNote),
        useSourceBottleImage: false,
      })
      return
    }

    mutation.mutate({
      name: values.name.trim(),
      brand: toText(values.brand),
      distilleryId: values.distilleryId,
      category: values.category,
      age: toNumber(values.age),
      abvPercent: toNumber(values.abv),
      volumeMl: toNumber(values.volume),
      barcode: toText(values.barcode),
      country: toText(values.country),
      region: toText(values.region),
      imageUrl: toText(values.imageUrl),
      description: toText(values.description),
      adminNote: toText(values.adminNote),
      useSourceBottleImage: request.sourceBottleId != null && useSourceImage,
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] w-[calc(100%-2rem)] max-w-[560px] overflow-y-auto sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>{t('productRequests.approveTitle')}</DialogTitle>
          <DialogDescription>{request.name}</DialogDescription>
        </DialogHeader>

        <div className="flex items-center justify-between rounded-md border border-input p-3">
          <Label htmlFor={`link-mode-${request.id}`} className="cursor-pointer">
            {t('productRequests.linkExisting')}
          </Label>
          <Switch
            id={`link-mode-${request.id}`}
            checked={linkMode}
            onCheckedChange={checked => {
              setLinkMode(checked)
              if (!checked) setLinkedProduct(null)
            }}
          />
        </div>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            {linkMode ? (
              <div className="space-y-2">
                {/* ProductSelect renders its own label — it owns the trigger id that htmlFor needs. */}
                <ProductSelect onSelect={setLinkedProduct} />
                {linkedProduct ? (
                  <div className="rounded-md border border-primary/40 bg-primary/5 px-3 py-2 text-sm text-primary">
                    {linkedProduct.name}
                  </div>
                ) : (
                  <p className="text-xs text-muted-foreground">{t('productRequests.pickProduct')}</p>
                )}
              </div>
            ) : (
              <>
                <p className="text-xs text-muted-foreground">
                  {t('productRequests.keepOnEmptyHint')}
                </p>

                <FormField
                  control={form.control}
                  name="name"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('addBottle.name')}</FormLabel>
                      <FormControl>
                        <Input className="h-9" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <div className="grid grid-cols-2 gap-3">
                  <FormField
                    control={form.control}
                    name="category"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{t('addBottle.category')}</FormLabel>
                        <Select value={field.value} onValueChange={field.onChange}>
                          <FormControl>
                            <SelectTrigger className="h-9 w-full">
                              <SelectValue />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
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
                  <FormField
                    control={form.control}
                    name="brand"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{t('productRequests.brand')}</FormLabel>
                        <FormControl>
                          <Input className="h-9" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                <FormField
                  control={form.control}
                  name="distilleryId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('addBottle.distillery')}</FormLabel>
                      <DistillerySelect
                        value={field.value}
                        onChange={field.onChange}
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

                <div className="grid grid-cols-3 gap-3">
                  <FormField
                    control={form.control}
                    name="country"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{t('productRequests.country')}</FormLabel>
                        <FormControl>
                          <Input className="h-9" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="region"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{t('productRequests.region')}</FormLabel>
                        <FormControl>
                          <Input className="h-9" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="barcode"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{t('productRequests.barcode')}</FormLabel>
                        <FormControl>
                          <Input className="h-9" {...field} />
                        </FormControl>
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
                      <FormLabel>{t('productRequests.imageUrl')}</FormLabel>
                      <FormControl>
                        <Input className="h-9" placeholder="https://…" {...field} />
                      </FormControl>
                      <FormMessage />
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
                        <Textarea rows={3} {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                {request.sourceBottleId && (
                  <div className="flex items-center justify-between rounded-md border border-input p-3">
                    <Label htmlFor={`source-image-${request.id}`} className="cursor-pointer">
                      {t('productRequests.useSourceImage')}
                    </Label>
                    <Switch
                      id={`source-image-${request.id}`}
                      checked={useSourceImage}
                      onCheckedChange={setUseSourceImage}
                    />
                  </div>
                )}
              </>
            )}

            <FormField
              control={form.control}
              name="adminNote"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('productRequests.adminNote')}</FormLabel>
                  <FormControl>
                    <Textarea rows={2} placeholder={t('productRequests.adminNotePlaceholder')} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
                disabled={mutation.isPending}
              >
                {t('productRequests.cancel')}
              </Button>
              <Button type="submit" disabled={mutation.isPending || (linkMode && !linkedProduct)}>
                {mutation.isPending ? t('productRequests.saving') : t('productRequests.approve')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}

function RejectDialog({
  request,
  open,
  onOpenChange,
}: {
  request: ProductRequest
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [adminNote, setAdminNote] = useState('')

  const mutation = useMutation({
    mutationFn: () => rejectProductRequest(request.id, { adminNote: toText(adminNote) }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['productRequests'] })
      toast.success(t('productRequests.rejectSuccess'))
      onOpenChange(false)
    },
    onError: () => toast.error(t('productRequests.error')),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[calc(100%-2rem)] max-w-[440px] sm:max-w-[440px]">
        <DialogHeader>
          <DialogTitle>{t('productRequests.rejectTitle')}</DialogTitle>
          <DialogDescription>{request.name}</DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <Label htmlFor={`reject-note-${request.id}`}>{t('productRequests.adminNote')}</Label>
          <Textarea
            id={`reject-note-${request.id}`}
            rows={3}
            value={adminNote}
            onChange={e => setAdminNote(e.target.value)}
            placeholder={t('productRequests.adminNotePlaceholder')}
          />
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={mutation.isPending}
          >
            {t('productRequests.cancel')}
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending}
          >
            {mutation.isPending ? t('productRequests.saving') : t('productRequests.reject')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function RequestCard({ request }: { request: ProductRequest }) {
  const { t } = useTranslation()
  const [approveOpen, setApproveOpen] = useState(false)
  const [rejectOpen, setRejectOpen] = useState(false)

  return (
    <Card>
      <CardContent className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-[220px] flex-1">
          <div className="mb-1.5 flex flex-wrap items-center gap-2.5">
            <span className="font-heading text-lg font-semibold text-foreground">{request.name}</span>
            <Badge variant={STATUS_VARIANTS[request.status]}>
              {t(`productRequests.status.${request.status}`)}
            </Badge>
          </div>

          <div className="mb-2 text-xs uppercase tracking-wide text-muted-foreground">
            {requestMeta(request, t)}
          </div>

          <div className="text-sm text-muted-foreground">
            {t('productRequests.requestedBy', { name: request.requesterDisplayName })} ·{' '}
            {formatDate(request.createdAt)}
          </div>

          {request.userNote && (
            <p className="mt-2.5 text-sm italic leading-relaxed text-foreground/90">
              <span className="not-italic text-muted-foreground">
                {t('productRequests.userNote')}:
              </span>{' '}
              “{request.userNote}”
            </p>
          )}

          {request.adminNote && (
            <p className="mt-2.5 text-sm leading-relaxed text-muted-foreground">
              {t('productRequests.adminNote')}: {request.adminNote}
            </p>
          )}
        </div>

        {request.status === 'Pending' && (
          <div className="flex gap-2">
            <Button size="sm" onClick={() => setApproveOpen(true)}>
              <Check className="size-3.5" />
              {t('productRequests.approve')}
            </Button>
            <Button size="sm" variant="destructive" onClick={() => setRejectOpen(true)}>
              <X className="size-3.5" />
              {t('productRequests.reject')}
            </Button>
          </div>
        )}
      </CardContent>

      {approveOpen && (
        <ApproveDialog request={request} open={approveOpen} onOpenChange={setApproveOpen} />
      )}
      {rejectOpen && (
        <RejectDialog request={request} open={rejectOpen} onOpenChange={setRejectOpen} />
      )}
    </Card>
  )
}

function RequestList({ status }: { status: ProductRequestStatus }) {
  const { t } = useTranslation()

  const { data: requests = [], isLoading, isError } = useQuery({
    queryKey: ['productRequests', 'admin', status],
    queryFn: () => getProductRequests(status),
  })

  if (isLoading) return <StateMessage text={t('productRequests.loading')} />
  if (isError) return <StateMessage text={t('productRequests.error')} error />
  if (requests.length === 0) return <StateMessage text={t('productRequests.empty')} />

  return (
    <div className="flex flex-col gap-4">
      {requests.map(request => (
        <RequestCard key={request.id} request={request} />
      ))}
    </div>
  )
}

export default function ProductRequestsAdminPage() {
  const { t } = useTranslation()

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[880px] px-6 py-10 sm:px-10">
        <div className="mb-7">
          <div className="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t('nav.productRequests')}
          </div>
          <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">
            {t('productRequests.title')}
          </h1>
        </div>

        <Tabs defaultValue="Pending" className="gap-6">
          <TabsList variant="line">
            {STATUSES.map(status => (
              <TabsTrigger key={status} value={status}>
                {t(`productRequests.status.${status}`)}
              </TabsTrigger>
            ))}
          </TabsList>
          {STATUSES.map(status => (
            <TabsContent key={status} value={status}>
              <RequestList status={status} />
            </TabsContent>
          ))}
        </Tabs>
      </main>
    </div>
  )
}
