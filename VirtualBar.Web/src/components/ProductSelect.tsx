import { useEffect, useId, useState } from 'react'
import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { ChevronsUpDown, Loader2 } from 'lucide-react'
import { searchProducts } from '../api/productsApi'
import type { Product, SpiritCategory } from '../types'
import { CATEGORY_COLORS } from './BarShelf'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Command,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

interface Props {
  onSelect: (product: Product) => void
  category?: SpiritCategory
}

const MIN_CHARS = 2
const DEBOUNCE_MS = 250

// The API defaults to 20 when the caller omits a limit. A brand like Macallan has well over a hundred
// entries, so the default silently hid most of them.
const MAX_RESULTS = 50

// Ask for one more row than we render. If it comes back there genuinely are further matches, so the
// "refine your search" hint is only ever shown when it is true — a full page of exactly 50 is not the
// same thing as a truncated one. The API clamps to this value.
const PROBE_LIMIT = MAX_RESULTS + 1

function metaLine(product: Product, t: TFunction): string {
  return [
    product.distilleryName ?? product.brand ?? null,
    product.volumeMl != null ? t('products.volumeSuffix', { volume: product.volumeMl }) : null,
    CATEGORY_COLORS[product.category]?.label ?? product.category,
  ]
    .filter(Boolean)
    .join(' · ')
}

export default function ProductSelect({ onSelect, category }: Props) {
  const { t } = useTranslation()
  const triggerId = useId()
  const [open, setOpen] = useState(false)
  const [term, setTerm] = useState('')
  const [debouncedTerm, setDebouncedTerm] = useState('')

  // Hiding a broken thumbnail by writing to element.style behind React's back leaves the DOM and the
  // render output disagreeing: the next render of that row brings the broken icon straight back. Keyed
  // by product id, so the set stays correct as results come and go.
  const [brokenImages, setBrokenImages] = useState<ReadonlySet<string>>(() => new Set())

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedTerm(term.trim()), DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [term])

  const enabled = debouncedTerm.length >= MIN_CHARS

  const { data: fetched = [], isFetching, isError } = useQuery({
    queryKey: ['products', 'search', debouncedTerm, category ?? 'all', PROBE_LIMIT],
    queryFn: () => searchProducts(debouncedTerm, category, PROBE_LIMIT),
    enabled,
    placeholderData: keepPreviousData,
  })

  const hasMore = fetched.length > MAX_RESULTS
  const products = hasMore ? fetched.slice(0, MAX_RESULTS) : fetched

  // `isLoading` is `isPending && isFetching`, and keepPreviousData keeps the status at 'success' across
  // key changes — so it is only ever true for the very first search. Everything after that would render
  // the previous term's results as if they belonged to the current one, and let the user click them.
  // isFetching covers the in-flight request; the term comparison covers the debounce window before it.
  const isStale = isFetching || term.trim() !== debouncedTerm

  // The popover is placed while the list is still empty, and Radix does not re-measure when the results
  // arrive and make it ~200px taller. Opened near the bottom of the scrolled sheet it therefore flips
  // upwards using the empty height and ends up hanging off the screen. Floating UI's auto-update does
  // listen for window resize, so one synthetic event after the content settles is enough to re-place it.
  useEffect(() => {
    if (open)
      window.dispatchEvent(new Event('resize'))
  }, [open, products.length, isStale])

  const handleOpenChange = (next: boolean) => {
    setOpen(next)
    if (!next) {
      setTerm('')
      setDebouncedTerm('')
    }
  }

  return (
    // The label lives here rather than at the call sites: it needs htmlFor pointing at the trigger,
    // and only this component knows the generated id. Both call sites used to render a bare <Label>
    // that was associated with nothing at all.
    <div className="space-y-2">
      <Label htmlFor={triggerId}>{t('products.searchLabel')}</Label>

      <div className="relative w-full">
      {/*
        modal is required, not cosmetic: this select is rendered inside the add-bottle Sheet, and a
        Radix Dialog mounts react-remove-scroll, which cancels wheel events outside its locked
        subtree. The popover portals to document.body — outside it — so without modal the list can be
        clicked and keyboard-scrolled but never mouse-wheeled. modal gives the popover its own lock
        with its content as the allowed shard.
      */}
      <Popover open={open} onOpenChange={handleOpenChange} modal>
        <PopoverTrigger asChild>
          <Button
            id={triggerId}
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            aria-haspopup="listbox"
            className="h-9 w-full justify-start pr-8 font-normal text-muted-foreground"
          >
            <span className="truncate">{t('products.searchPlaceholder')}</span>
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-(--radix-popover-trigger-width) p-0">
          {/* cmdk renders `label` as a visually hidden <label> tied to its own input, which otherwise
              has role="combobox" with no accessible name at all. */}
          <Command shouldFilter={false} label={t('products.searchLabel')}>
            <CommandInput
              value={term}
              onValueChange={setTerm}
              placeholder={t('products.searchPlaceholder')}
            />
            {/* Everything below that is not a result carries role="presentation": CommandList is a
                listbox, and a listbox may only own options. Status text announced as an option is
                both invalid and confusing to a screen reader. */}
            <CommandList>
              {!enabled && (
                <div role="presentation" className="px-3 py-6 text-center text-xs text-muted-foreground">
                  {t('products.minChars')}
                </div>
              )}

              {/* Only take the full spinner block when there is nothing to keep on screen; otherwise
                  the previous results stay visible but dimmed, which avoids the list jumping. */}
              {enabled && isStale && products.length === 0 && (
                <div role="presentation" className="flex justify-center px-3 py-6">
                  <Loader2 className="size-4 animate-spin text-primary" />
                  <span className="sr-only">{t('products.searchLabel')}</span>
                </div>
              )}

              {enabled && !isStale && isError && (
                <div
                  role="presentation"
                  aria-live="polite"
                  className="px-3 py-6 text-center text-xs text-destructive"
                >
                  {t('products.error')}
                </div>
              )}

              {enabled && !isStale && !isError && products.length === 0 && (
                <div role="presentation" aria-live="polite" className="space-y-2 px-3 py-5 text-center">
                  <div className="text-xs text-muted-foreground">{t('products.noResults')}</div>
                  <div className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-left text-xs leading-relaxed text-primary/90">
                    {t('products.requestNotice')}
                  </div>
                </div>
              )}

              {enabled && !isError && products.length > 0 && (
                // pointer-events-none is the important half: it stops a click landing on a result that
                // belongs to a term the user has already moved on from, which would link the bottle to
                // the wrong catalog product.
                <CommandGroup
                  className={isStale ? 'pointer-events-none opacity-50' : undefined}
                  aria-busy={isStale || undefined}
                >
                  {products.map(product => (
                    <CommandItem
                      key={product.id}
                      value={product.id}
                      onSelect={() => {
                        onSelect(product)
                        handleOpenChange(false)
                      }}
                    >
                      <div className="flex min-w-0 flex-1 items-center gap-2.5">
                        {product.imageUrl && !brokenImages.has(product.id) && (
                          <img
                            src={product.imageUrl}
                            alt=""
                            className="size-8 shrink-0 rounded-md object-cover"
                            onError={() =>
                              setBrokenImages(previous => new Set(previous).add(product.id))
                            }
                          />
                        )}
                        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                          <span className="truncate">
                            {product.name}
                            {product.age != null && (
                              <span className="ml-1.5 text-muted-foreground">
                                {t('products.ageSuffix', { age: product.age })}
                              </span>
                            )}
                          </span>
                          <span className="truncate text-[10px] uppercase tracking-wide text-muted-foreground">
                            {metaLine(product, t)}
                          </span>
                        </div>
                      </div>
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}

              {enabled && !isStale && !isError && hasMore && (
                <div
                  role="presentation"
                  className="border-t border-border/50 px-3 py-2 text-center text-[10px] leading-relaxed text-muted-foreground"
                >
                  {t('products.truncated', { max: MAX_RESULTS })}
                </div>
              )}
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>

        <ChevronsUpDown className="pointer-events-none absolute top-1/2 right-2.5 size-3.5 -translate-y-1/2 opacity-50" />
      </div>
    </div>
  )
}
