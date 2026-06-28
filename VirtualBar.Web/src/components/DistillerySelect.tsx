import { useState } from 'react'
import type { CSSProperties } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ChevronsUpDown, X } from 'lucide-react'
import { getDistilleries } from '../api/distilleriesApi'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

interface Props {
  value: string | null
  onChange: (id: string | null, name: string | null) => void
  placeholder?: string
  category?: string
  style?: CSSProperties
  inputStyle?: CSSProperties
}

export default function DistillerySelect({ value, onChange, placeholder, category, style }: Props) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  const { data: distilleries = [] } = useQuery({
    queryKey: ['distilleries', category ?? 'all'],
    queryFn: () => getDistilleries(category),
    staleTime: 5 * 60_000,
  })

  const selected = distilleries.find(d => d.id === value) ?? null

  return (
    <div className="relative w-full" style={style}>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="outline"
            role="combobox"
            aria-expanded={open}
            className={cn(
              'h-9 w-full justify-start font-normal',
              !selected && 'text-muted-foreground',
              value ? 'pr-14' : 'pr-8',
            )}
          >
            <span className="truncate">
              {selected ? selected.name : (placeholder ?? t('distillerySelect.placeholder'))}
            </span>
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-(--radix-popover-trigger-width) p-0">
          <Command>
            <CommandInput placeholder={t('distillerySelect.placeholder')} />
            <CommandList>
              <CommandEmpty>{t('distillerySelect.noOptions')}</CommandEmpty>
              <CommandGroup>
                {distilleries.map(d => (
                  <CommandItem
                    key={d.id}
                    value={d.name}
                    data-checked={value === d.id ? 'true' : undefined}
                    onSelect={() => {
                      onChange(d.id, d.name)
                      setOpen(false)
                    }}
                  >
                    <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate">{d.name}</span>
                      {(d.region || d.country) && (
                        <span className="truncate text-[10px] uppercase tracking-wide text-muted-foreground">
                          {[d.region, d.country].filter(Boolean).join(', ')}
                        </span>
                      )}
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>

      <ChevronsUpDown className="pointer-events-none absolute top-1/2 right-2.5 size-3.5 -translate-y-1/2 opacity-50" />

      {value && (
        <button
          type="button"
          aria-label={t('distillerySelect.clear')}
          onClick={e => {
            e.stopPropagation()
            onChange(null, null)
          }}
          className="absolute top-1/2 right-7 -translate-y-1/2 rounded-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <X className="size-3.5" />
        </button>
      )}
    </div>
  )
}
