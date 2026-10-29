import type { BottleCondition, SpiritCategory } from '../types'

export const CATEGORY_COLORS: Record<SpiritCategory, { body: string; glass: string; glow: string; label: string }> = {
  Whisky:  { body: '#5C3A00', glass: '#C8820A', glow: '#C8820A', label: 'Whisky' },
  Rum:     { body: '#5A1008', glass: '#D42020', glow: '#E83020', label: 'Rum' },
  Cognac:  { body: '#4A2008', glass: '#C86030', glow: '#D87040', label: 'Cognac' },
  Vodka:   { body: '#1A2A3A', glass: '#90B8D8', glow: '#A0C8E8', label: 'Vodka' },
  Gin:     { body: '#102818', glass: '#4A9A6A', glow: '#5AAA7A', label: 'Gin' },
  Tequila: { body: '#5A4A00', glass: '#D4A820', glow: '#E4B830', label: 'Tequila' },
  Brandy:  { body: '#4A2008', glass: '#C84818', glow: '#D85828', label: 'Brandy' },
  Other:   { body: '#201040', glass: '#8060C8', glow: '#9070D8', label: 'Other' },
}

export const LIQUID_LEVEL: Record<BottleCondition, number> = {
  Sealed: 0.88,
  Opened: 0.45,
  Empty: 0,
}

export const CAP_COLOR: Record<BottleCondition, string> = {
  Sealed: '#C9A84C',
  Opened: '#5A3A1A',
  Empty: '#1A0C06',
}
