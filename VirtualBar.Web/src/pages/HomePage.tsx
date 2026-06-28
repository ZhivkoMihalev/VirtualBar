import { useMemo, useRef, useState } from 'react'
import type { ChangeEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Plus, Pencil, Trash2, ChevronDown, Upload, Loader2, X, Newspaper } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import {
  createNewsPost,
  updateNewsPost,
  deleteNewsPost,
  getNewsPost,
  uploadNewsCover,
} from '../api/newsApi'
import { getFeed } from '../api/feedApi'
import type { CreateNewsPostPayload, FeedItem, NewsPost } from '../types'
import NavBar from '../components/NavBar'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Sheet, SheetClose, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
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

type LangCode = 'bg' | 'en'

const PREVIEW_LENGTH = 280

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })
}

const makeSchema = (t: TFn) =>
  z.object({
    coverImageUrl: z.string(),
    bgTitle: z.string().trim().min(1, t('home.postValidationRequired')),
    bgContent: z.string(),
    enTitle: z.string(),
    enContent: z.string(),
  })

type Values = z.infer<ReturnType<typeof makeSchema>>

function initialToValues(initial?: NewsPost): Values {
  const bg = initial?.translations.find(tr => tr.languageCode === 'bg')
  const en = initial?.translations.find(tr => tr.languageCode === 'en')
  return {
    coverImageUrl: initial?.coverImageUrl ?? '',
    bgTitle: bg?.title ?? '',
    bgContent: bg?.content ?? '',
    enTitle: en?.title ?? '',
    enContent: en?.content ?? '',
  }
}

function Hero() {
  const { t } = useTranslation()
  return (
    <header className="px-6 pb-8 pt-12 text-center sm:pt-16">
      <div className="mb-4 text-xs uppercase tracking-[0.35em] text-muted-foreground">{t('hero.vol')}</div>
      <h1 className="font-heading text-4xl font-bold tracking-wide text-primary sm:text-5xl">
        {t('hero.title')}
      </h1>
      <div className="mx-auto my-6 h-0.5 w-44 bg-gradient-to-r from-transparent via-primary to-transparent" />
      <p className="mx-auto max-w-xl text-lg italic text-primary/90">{t('hero.subtitle')}</p>
    </header>
  )
}

function SkeletonCard() {
  return (
    <Card className="gap-4 border-l-[3px] border-l-primary p-7">
      <Skeleton className="h-3 w-28" />
      <Skeleton className="h-7 w-3/4" />
      <Skeleton className="h-3.5 w-full" />
      <Skeleton className="h-3.5 w-5/6" />
      <Skeleton className="h-3.5 w-3/5" />
    </Card>
  )
}

function NewsPostCard({
  post,
  isAdmin,
  onEdit,
  onDelete,
}: {
  post: NewsPost
  isAdmin: boolean
  onEdit: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()
  const [expanded, setExpanded] = useState(false)

  const isLong = post.content.length > PREVIEW_LENGTH
  const lastSpace = post.content.lastIndexOf(' ', PREVIEW_LENGTH)
  const displayContent =
    !expanded && isLong
      ? post.content.slice(0, lastSpace > 0 ? lastSpace : PREVIEW_LENGTH) + '…'
      : post.content

  return (
    <Card className="group gap-0 overflow-hidden border-l-[3px] border-l-primary p-0 transition-all hover:-translate-y-1 hover:shadow-xl">
      {post.coverImageUrl && (
        <div className="h-56 overflow-hidden">
          <img
            src={post.coverImageUrl}
            alt={post.title}
            className="size-full object-cover transition-transform duration-500 group-hover:scale-[1.04]"
          />
        </div>
      )}

      <div className="space-y-3 p-7">
        <div className="flex items-center gap-2.5 text-xs font-medium uppercase tracking-wide text-primary">
          <span className="h-px w-6 bg-primary" />
          {t('hero.title')}
        </div>

        <h2 className="font-heading text-2xl font-semibold text-foreground">{post.title}</h2>

        <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
          {displayContent}
        </p>

        {isLong && (
          <Button
            variant="link"
            size="sm"
            className="h-auto gap-1.5 px-0"
            onClick={() => setExpanded(e => !e)}
          >
            {expanded ? t('home.readLess') : t('home.readMore')}
            <ChevronDown className={cn('size-3.5 transition-transform', expanded && 'rotate-180')} />
          </Button>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border px-7 py-4">
        <div className="text-sm italic text-muted-foreground">
          {t('home.authorBy', { name: post.authorDisplayName })} · {formatDate(post.createdAt)}
        </div>

        {isAdmin && (
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={onEdit} aria-label={t('home.editBtn')}>
              <Pencil className="size-3.5" />
              {t('home.editBtn')}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={onDelete}
              aria-label={t('home.deleteBtn')}
              className="border-destructive/40 text-destructive hover:bg-destructive/10"
            >
              <Trash2 className="size-3.5" />
              {t('home.deleteBtn')}
            </Button>
          </div>
        )}
      </div>
    </Card>
  )
}

function BottleThumb({ url, category }: { url?: string; category?: string }) {
  if (url) {
    return (
      <div className="size-20 shrink-0 overflow-hidden rounded border border-border">
        <img src={url} alt={category ?? 'Bottle'} className="size-full object-cover" />
      </div>
    )
  }

  return (
    <div className="flex size-20 shrink-0 items-center justify-center rounded border border-border bg-primary/5 font-heading text-3xl text-primary">
      {category ? category.charAt(0).toUpperCase() : '\u{1F943}'}
    </div>
  )
}

function BottleActivityCard({ item }: { item: FeedItem }) {
  const { t } = useTranslation()
  const forSale = item.type === 'ForSale'
  const headerText = forSale ? t('home.listedForSale') : t('home.addedToCollection')
  const linkTo = forSale ? '/marketplace' : `/bar/${item.bottleUserId}`

  return (
    <Card className="group flex flex-row items-center gap-4 border-l-[3px] border-l-primary p-5 transition-all hover:-translate-y-1 hover:shadow-xl">
      <div className="min-w-0 flex-1">
        <div className="mb-2 flex flex-wrap items-center gap-2 text-sm italic text-muted-foreground">
          <Link to={`/bar/${item.bottleUserId}`} className="text-primary not-italic hover:underline">
            {item.bottleUserDisplayName}
          </Link>
          <span>{headerText}</span>
          {forSale && (
            <Badge className="text-[10px] uppercase tracking-wide">{t('home.forSale')}</Badge>
          )}
        </div>

        <Link to={linkTo} className="hover:underline">
          <h3 className="font-heading text-lg font-semibold text-foreground">{item.bottleName}</h3>
        </Link>

        <div className="mt-2 flex flex-wrap items-center gap-3">
          {item.bottleCategory && (
            <Badge variant="outline" className="uppercase">
              {item.bottleCategory}
            </Badge>
          )}
          {forSale && item.askingPrice != null && (
            <span className="text-base font-semibold text-primary">
              {item.currency} {item.askingPrice.toFixed(2)}
            </span>
          )}
          <span className="text-xs italic text-muted-foreground">{formatDate(item.timestamp)}</span>
        </div>
      </div>

      <BottleThumb url={item.bottlePrimaryImageUrl} category={item.bottleCategory} />
    </Card>
  )
}

function PostFormSheetBody({
  initial,
  isEdit,
  pending,
  isError,
  onSubmit,
}: {
  initial?: NewsPost
  isEdit: boolean
  pending: boolean
  isError: boolean
  onSubmit: (payload: CreateNewsPostPayload) => void
}) {
  const { t } = useTranslation()
  const schema = useMemo(() => makeSchema(t), [t])
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: initialToValues(initial),
  })

  const [activeTab, setActiveTab] = useState<LangCode>('bg')
  const [coverUploading, setCoverUploading] = useState(false)
  const coverInputRef = useRef<HTMLInputElement>(null)

  async function handleCoverUpload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setCoverUploading(true)
    try {
      const url = await uploadNewsCover(file)
      form.setValue('coverImageUrl', url, { shouldDirty: true })
    } catch {
      toast.error(t('home.coverUploadError'))
    } finally {
      setCoverUploading(false)
      e.target.value = ''
    }
  }

  const onValid = (v: Values) => {
    const translations: CreateNewsPostPayload['translations'] = []
    const bgTitle = v.bgTitle.trim()
    const bgContent = v.bgContent.trim()
    if (bgTitle || bgContent) translations.push({ languageCode: 'bg', title: bgTitle, content: bgContent })
    const enTitle = v.enTitle.trim()
    const enContent = v.enContent.trim()
    if (enTitle || enContent) translations.push({ languageCode: 'en', title: enTitle, content: enContent })

    onSubmit({ coverImageUrl: v.coverImageUrl.trim() || undefined, translations })
  }

  const onInvalid = () => {
    if (form.formState.errors.bgTitle) setActiveTab('bg')
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onValid, onInvalid)} className="space-y-5 px-6 pb-8">
        <Tabs value={activeTab} onValueChange={v => setActiveTab(v as LangCode)}>
          <TabsList className="w-full">
            <TabsTrigger value="bg" className="flex-1">
              {t('lang.bg')}
              {form.formState.errors.bgTitle && (
                <span className="ml-1 inline-block size-1.5 rounded-full bg-destructive" />
              )}
            </TabsTrigger>
            <TabsTrigger value="en" className="flex-1">
              {t('lang.en')}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="bg" className="space-y-4 pt-4">
            <FormField
              control={form.control}
              name="bgTitle"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('home.postTitleLabel')}</FormLabel>
                  <FormControl>
                    <Input className="h-9" placeholder={t('home.postTitlePlaceholder')} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="bgContent"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('home.postContentLabel')}</FormLabel>
                  <FormControl>
                    <Textarea rows={10} placeholder={t('home.postContentPlaceholder')} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </TabsContent>

          <TabsContent value="en" className="space-y-4 pt-4">
            <FormField
              control={form.control}
              name="enTitle"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('home.postTitleLabel')}</FormLabel>
                  <FormControl>
                    <Input className="h-9" placeholder={t('home.postTitlePlaceholder')} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="enContent"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('home.postContentLabel')}</FormLabel>
                  <FormControl>
                    <Textarea rows={10} placeholder={t('home.postContentPlaceholder')} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </TabsContent>
        </Tabs>

        <FormField
          control={form.control}
          name="coverImageUrl"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('home.postCoverLabel')}</FormLabel>

              {field.value && (
                <div className="relative">
                  <img
                    src={field.value}
                    alt="cover preview"
                    className="max-h-44 w-full rounded-md border border-border object-cover"
                  />
                  <Button
                    type="button"
                    variant="secondary"
                    size="icon-xs"
                    onClick={() => field.onChange('')}
                    className="absolute right-2 top-2"
                    aria-label={t('home.removeCover')}
                  >
                    <X className="size-3.5" />
                  </Button>
                </div>
              )}

              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-9 shrink-0"
                  disabled={coverUploading}
                  onClick={() => coverInputRef.current?.click()}
                >
                  {coverUploading ? (
                    <Loader2 className="size-3.5 animate-spin" />
                  ) : (
                    <Upload className="size-3.5" />
                  )}
                  {t('home.uploadCover')}
                </Button>
                <FormControl>
                  <Input
                    className="h-9 flex-1"
                    placeholder={t('home.postCoverUrlPlaceholder')}
                    {...field}
                  />
                </FormControl>
              </div>

              <input
                ref={coverInputRef}
                type="file"
                accept="image/jpeg,image/png,image/webp,image/gif"
                className="hidden"
                onChange={handleCoverUpload}
                disabled={coverUploading}
              />
            </FormItem>
          )}
        />

        {isError && (
          <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {t('home.errorSubmit')}
          </div>
        )}

        <div className="space-y-2">
          <Button type="submit" size="lg" className="h-10 w-full" disabled={pending}>
            {pending ? '…' : isEdit ? t('home.updatePost') : t('home.submitPost')}
          </Button>
          <SheetClose asChild>
            <Button type="button" variant="outline" size="lg" className="h-10 w-full">
              {t('home.cancelPost')}
            </Button>
          </SheetClose>
        </div>
      </form>
    </Form>
  )
}

export default function HomePage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const isAdmin = user?.isAdmin === true
  const lang = i18n.language?.startsWith('bg') ? 'bg' : 'en'

  const [showCreatePanel, setShowCreatePanel] = useState(false)
  const [editingPost, setEditingPost] = useState<NewsPost | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)

  async function handleEditPost(postId: string) {
    try {
      const full = await getNewsPost(postId, lang)
      setEditingPost(full)
    } catch {
      toast.error(t('home.errorSubmit'))
    }
  }

  const { data: feed = [], isLoading, isError } = useQuery({
    queryKey: ['feed', lang],
    queryFn: () => getFeed(0, 50, lang),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['feed'] })

  const createMutation = useMutation({
    mutationFn: createNewsPost,
    onSuccess: () => {
      invalidate()
      setShowCreatePanel(false)
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: CreateNewsPostPayload }) =>
      updateNewsPost(id, payload),
    onSuccess: () => {
      invalidate()
      setEditingPost(null)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteNewsPost,
    onSuccess: () => {
      invalidate()
      setConfirmDelete(null)
    },
  })

  const editorOpen = showCreatePanel || editingPost !== null

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <Hero />

      <main className="mx-auto max-w-[760px] px-6 pb-20">
        {isAdmin && (
          <div className="mb-7 flex justify-end">
            <Button onClick={() => setShowCreatePanel(true)}>
              <Plus className="size-4" />
              {t('home.newPost')}
            </Button>
          </div>
        )}

        {isLoading && (
          <div className="flex flex-col gap-7">
            <SkeletonCard />
            <SkeletonCard />
            <SkeletonCard />
          </div>
        )}

        {isError && !isLoading && (
          <div className="py-16 text-center text-sm uppercase tracking-widest text-destructive">
            {t('home.errorLoading')}
          </div>
        )}

        {!isLoading && !isError && feed.length === 0 && (
          <div className="py-20 text-center">
            <Newspaper className="mx-auto mb-4 size-10 text-muted-foreground/50" />
            <p className="mx-auto max-w-sm text-xl italic text-muted-foreground">{t('home.noNews')}</p>
          </div>
        )}

        {!isLoading && !isError && feed.length > 0 && (
          <div className="flex flex-col gap-7">
            {feed.map(item => {
              if (item.type === 'News' && item.postId) {
                const post: NewsPost = {
                  id: item.postId,
                  title: item.postTitle ?? '',
                  content: item.postContent ?? '',
                  coverImageUrl: item.postCoverImageUrl,
                  authorId: '',
                  authorDisplayName: item.postAuthorDisplayName ?? '',
                  createdAt: item.timestamp,
                  updatedAt: item.timestamp,
                  translations: [],
                }
                return (
                  <NewsPostCard
                    key={`news-${item.postId}`}
                    post={post}
                    isAdmin={isAdmin}
                    onEdit={() => handleEditPost(item.postId!)}
                    onDelete={() => setConfirmDelete(item.postId!)}
                  />
                )
              }

              return <BottleActivityCard key={`${item.type}-${item.bottleId}`} item={item} />
            })}
          </div>
        )}
      </main>

      <Sheet
        open={editorOpen}
        onOpenChange={o => {
          if (!o) {
            setShowCreatePanel(false)
            setEditingPost(null)
          }
        }}
      >
        <SheetContent
          side="right"
          className="overflow-y-auto data-[side=right]:w-full data-[side=right]:sm:max-w-[480px]"
        >
          <SheetHeader>
            <SheetTitle>
              {editingPost ? t('home.editPostTitle') : t('home.createPostTitle')}
            </SheetTitle>
            <SheetDescription className="sr-only">
              {editingPost ? t('home.editPostTitle') : t('home.createPostTitle')}
            </SheetDescription>
          </SheetHeader>
          <PostFormSheetBody
            key={editingPost ? editingPost.id : 'create'}
            initial={editingPost ?? undefined}
            isEdit={editingPost !== null}
            pending={editingPost ? updateMutation.isPending : createMutation.isPending}
            isError={editingPost ? updateMutation.isError : createMutation.isError}
            onSubmit={payload => {
              if (editingPost) updateMutation.mutate({ id: editingPost.id, payload })
              else createMutation.mutate(payload)
            }}
          />
        </SheetContent>
      </Sheet>

      <AlertDialog
        open={confirmDelete !== null}
        onOpenChange={o => {
          if (!o) setConfirmDelete(null)
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('home.confirmDelete')}</AlertDialogTitle>
            <AlertDialogDescription className="sr-only">{t('home.confirmDelete')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteMutation.isPending}>
              {t('home.cancelBtn')}
            </AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              className="bg-destructive text-white hover:bg-destructive/90 dark:bg-destructive dark:hover:bg-destructive/90"
              disabled={deleteMutation.isPending}
              onClick={e => {
                e.preventDefault()
                if (confirmDelete) deleteMutation.mutate(confirmDelete)
              }}
            >
              {deleteMutation.isPending ? '...' : t('home.confirmDeleteBtn')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
