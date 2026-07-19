import type { CSSProperties } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Wine, Boxes, Library, Warehouse, Trophy, Crown,
  Compass, Globe, Gem, Heart, Sparkles, Flame,
  UserPlus, Users, TrendingUp, Tag, Handshake, ShoppingBag,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import type { BadgeType } from '../types'

const BADGE_ICONS: Record<BadgeType, LucideIcon> = {
  FirstBottle: Wine,
  Collector5: Boxes,
  Collector10: Library,
  Collector25: Warehouse,
  Collector50: Trophy,
  Collector100: Crown,
  Explorer3: Compass,
  Explorer5: Globe,
  LimitedHunter: Gem,
  Liked10: Heart,
  Liked50: Sparkles,
  Liked100: Flame,
  FirstFollower: UserPlus,
  Popular10: Users,
  Influencer50: TrendingUp,
  FirstListing: Tag,
  FirstSale: Handshake,
  FirstPurchase: ShoppingBag,
}

const GOLD = '#C9A84C'
const GOLD_LIGHT = '#E8C870'

const rootStyle: CSSProperties = {
  display: 'inline-flex',
  flexDirection: 'column',
  alignItems: 'center',
  gap: 8,
  width: '100%',
  minWidth: 0,
}

const medallionStyle: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  borderRadius: '50%',
  flexShrink: 0,
}

const earnedMedallionStyle: CSSProperties = {
  border: `2px solid ${GOLD}`,
  color: GOLD_LIGHT,
  background: 'radial-gradient(circle at 50% 32%, rgba(201,168,76,0.22), rgba(16,8,4,0.95))',
  boxShadow: '0 0 14px rgba(201,168,76,0.35), inset 0 1px 3px rgba(232,200,112,0.22)',
}

const dimmedMedallionStyle: CSSProperties = {
  border: '2px solid rgba(168,162,158,0.6)',
  color: 'rgba(214,211,209,0.9)',
  background: 'rgba(28,25,23,0.85)',
  boxShadow: 'none',
}

const nameStyle: CSSProperties = {
  maxWidth: '100%',
  textAlign: 'center',
  fontSize: 11,
  lineHeight: 1.25,
  letterSpacing: '0.02em',
}

interface BadgeChipProps {
  badge: BadgeType
  earned: boolean
  awardedAt?: string | null
  size?: number
}

export default function BadgeChip({ badge, earned, awardedAt, size = 72 }: BadgeChipProps) {
  const { t } = useTranslation()
  const Icon = BADGE_ICONS[badge]
  const name = t(`badges.${badge}.name`)
  const description = t(`badges.${badge}.description`)
  const tooltip = earned && awardedAt
    ? `${description} · ${t('badges.earnedOn', { date: new Date(awardedAt).toLocaleDateString() })}`
    : description

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <div style={rootStyle}>
          <div
            style={{
              ...medallionStyle,
              width: size,
              height: size,
              ...(earned ? earnedMedallionStyle : dimmedMedallionStyle),
            }}
          >
            <Icon size={Math.round(size * 0.44)} strokeWidth={1.5} aria-hidden="true" />
          </div>
          <span style={{ ...nameStyle, color: earned ? GOLD_LIGHT : 'rgba(214,211,209,0.85)' }}>
            {name}
          </span>
        </div>
      </TooltipTrigger>
      <TooltipContent side="top" className="max-w-52 text-center">
        {tooltip}
      </TooltipContent>
    </Tooltip>
  )
}
