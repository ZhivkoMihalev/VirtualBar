import { useTranslation } from 'react-i18next'
import { ChevronDown } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation()

  const currentCode = i18n.language?.startsWith('bg') ? 'bg' : 'en'
  const currentLabel = currentCode === 'bg' ? 'BG' : 'EN'

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm">
          {currentLabel}
          <ChevronDown className="size-3 opacity-70" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuRadioGroup
          value={currentCode}
          onValueChange={(v) => i18n.changeLanguage(v)}
        >
          <DropdownMenuRadioItem value="bg">{t('lang.bg')}</DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="en">{t('lang.en')}</DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
