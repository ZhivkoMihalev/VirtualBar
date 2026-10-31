import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'

/**
 * A round category filter button. With a `color` (the category's glass color) the active pill takes
 * that tint; without one (the "All" pill) it uses the gold accent.
 */
export default function CategoryPill({
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
      aria-pressed={active}
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
