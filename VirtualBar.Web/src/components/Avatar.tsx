interface AvatarProps {
  displayName: string
  avatarUrl?: string | null
  size: number
}

export default function Avatar({ displayName, avatarUrl, size }: AvatarProps) {
  const initial = displayName.trim().charAt(0).toUpperCase() || '?'

  if (avatarUrl) {
    return (
      <img
        src={avatarUrl}
        alt={displayName}
        style={{
          width: size,
          height: size,
          borderRadius: '50%',
          objectFit: 'cover',
          border: '1.5px solid rgba(201,168,76,0.4)',
          flexShrink: 0,
        }}
      />
    )
  }

  return (
    <div
      style={{
        width: size,
        height: size,
        borderRadius: '50%',
        border: '1.5px solid rgba(201,168,76,0.4)',
        background: 'radial-gradient(ellipse at 50% 30%, rgba(201,168,76,0.15), rgba(10,5,2,0.6))',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'Playfair Display, serif',
        fontSize: Math.round(size * 0.42),
        color: '#E8C870',
        flexShrink: 0,
      }}
    >
      {initial}
    </div>
  )
}
