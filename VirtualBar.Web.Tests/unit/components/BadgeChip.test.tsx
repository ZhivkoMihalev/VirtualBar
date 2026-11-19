import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import BadgeChip from '@/components/BadgeChip'
import bg from '@/i18n/bg.json'
import type { BadgeType } from '@/types'
import { renderWithProviders } from '../testUtils'

describe('BadgeChip — the review badges (#125)', () => {
  it.each<BadgeType>(['FirstReview', 'Reviewer10', 'Reviewer50'])(
    'renders %s with its icon and translated name',
    badge => {
      const { container } = renderWithProviders(<BadgeChip badge={badge} earned={false} />)

      expect(screen.getByText(bg.badges[badge].name)).toBeInTheDocument()
      expect(container.querySelector('svg')).not.toBeNull()
    },
  )
})
