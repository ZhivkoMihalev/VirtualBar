import { Avatar as UIAvatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'

interface AvatarProps {
  displayName: string
  avatarUrl?: string | null
  size: number
}

export default function Avatar({ displayName, avatarUrl, size }: AvatarProps) {
  const initial = displayName.trim().charAt(0).toUpperCase() || '?'

  return (
    <UIAvatar className="shrink-0 border border-primary/40" style={{ width: size, height: size }}>
      <AvatarImage src={avatarUrl ?? undefined} alt={displayName} />
      <AvatarFallback
        className="bg-muted text-primary"
        style={{ fontSize: Math.round(size * 0.42) }}
      >
        {initial}
      </AvatarFallback>
    </UIAvatar>
  )
}
