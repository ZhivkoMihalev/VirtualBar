import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Search } from 'lucide-react'
import { searchUsers } from '../api/usersApi'
import type { UserSearchResult } from '../types'
import NavBar from '../components/NavBar'
import Avatar from '../components/Avatar'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(handle)
  }, [value, delay])

  return debounced
}

function CollectorCard({ collector }: { collector: UserSearchResult }) {
  const { t } = useTranslation()

  return (
    <Link to={`/bar/${collector.id}`} className="group block h-full">
      <Card className="h-full gap-3.5 p-6 transition-all group-hover:-translate-y-1 group-hover:shadow-lg group-hover:ring-primary/40">
        <div className="flex items-center gap-3.5">
          <Avatar displayName={collector.displayName} avatarUrl={collector.avatarUrl} size={56} />
          <div className="min-w-0">
            <div className="truncate font-heading text-lg font-semibold text-foreground">
              {collector.displayName}
            </div>
            {collector.country && (
              <div className="text-sm text-muted-foreground">{collector.country}</div>
            )}
          </div>
        </div>

        <p className="line-clamp-2 min-h-[2.75rem] text-sm italic leading-relaxed text-muted-foreground">
          {collector.bio || t('browse.defaultBio')}
        </p>

        <div className="flex items-center justify-between border-t border-border pt-3">
          <div className="flex gap-4 text-xs text-muted-foreground">
            <span>{t('browse.bottles', { count: collector.bottleCount })}</span>
            <span>{t('browse.followers', { count: collector.followerCount })}</span>
          </div>
          <span className="text-xs font-medium uppercase tracking-wide text-primary">
            {t('browse.viewBar')}
          </span>
        </div>
      </Card>
    </Link>
  )
}

export default function BrowsePage() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const query = useDebounced(search.trim(), 300)

  const { data: collectors = [], isLoading, isError } = useQuery({
    queryKey: ['users', query],
    queryFn: () => searchUsers(query || undefined),
  })

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[1100px] px-6 py-10">
        <div className="mb-7">
          <div className="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t('browse.discover')}
          </div>
          <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">{t('browse.title')}</h1>
          <div className="mt-1.5 text-lg italic text-primary/90">{t('browse.subtitle')}</div>
        </div>

        <div className="relative mb-9 max-w-md">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder={t('browse.searchPlaceholder')}
            className="h-10 pl-9"
          />
        </div>

        {isLoading && (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(300px,1fr))] gap-5">
            {Array.from({ length: 6 }).map((_, i) => (
              <Skeleton key={i} className="h-44 rounded-lg" />
            ))}
          </div>
        )}

        {isError && !isLoading && (
          <div className="mx-auto max-w-md rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-center text-sm text-destructive">
            {t('browse.error')}
          </div>
        )}

        {!isLoading && !isError && collectors.length === 0 && (
          <div className="py-16 text-center text-muted-foreground">{t('browse.noResults')}</div>
        )}

        {!isLoading && !isError && collectors.length > 0 && (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(300px,1fr))] gap-5">
            {collectors.map(collector => (
              <CollectorCard key={collector.id} collector={collector} />
            ))}
          </div>
        )}
      </main>
    </div>
  )
}
