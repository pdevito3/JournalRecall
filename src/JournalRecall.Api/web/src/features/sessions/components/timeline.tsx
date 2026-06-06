import { useEffect, useMemo, useState } from 'react'
import { Link, getRouteApi } from '@tanstack/react-router'
import { useSettings, useUpdateSettings } from '@/features/settings/useSettings'
import { buildSessionFilter, useSessionList, type TimelineSearch } from '@/features/sessions/useSessions'
import { KNOWN_MOODS, type SessionListItem } from '@/features/sessions/api'

const route = getRouteApi('/')

const COMMON_ZONES = [
  'UTC',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'Europe/London',
  'Europe/Paris',
  'Asia/Tokyo',
  'Australia/Sydney',
]

export function Timeline() {
  const [dayJump, setDayJump] = useState('') // YYYY-MM-DD, or '' for all
  // Topic/Person/Mood live in the URL (FE-009) so a filtered view is shareable and survives refresh.
  const { topic, person, mood } = route.useSearch()
  const navigate = route.useNavigate()
  const setFilters = (next: Partial<TimelineSearch>) =>
    navigate({ search: (prev) => ({ ...prev, ...next }) })

  // Build a QueryKit filter string from the metadata controls (server-side filtering).
  const filter = useMemo(() => buildSessionFilter({ topic, person, mood }), [topic, person, mood])

  const { data: sessions } = useSessionList(filter)
  const hasFilter = Boolean(filter)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <TimeZonePicker />
        <LocationToggle />
        <label className="flex items-center gap-2 text-sm text-muted">
          Jump to day
          <input
            type="date"
            value={dayJump}
            onChange={(e) => setDayJump(e.target.value)}
            className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
          {dayJump ? (
            <button type="button" className="text-accent hover:underline" onClick={() => setDayJump('')}>
              clear
            </button>
          ) : null}
        </label>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <input
          value={topic}
          onChange={(e) => setFilters({ topic: e.target.value })}
          placeholder="Filter by topic"
          className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
        <input
          value={person}
          onChange={(e) => setFilters({ person: e.target.value })}
          placeholder="Filter by person"
          className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
        <select
          value={mood}
          onChange={(e) => setFilters({ mood: e.target.value as TimelineSearch['mood'] })}
          className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <option value="">Any mood</option>
          {KNOWN_MOODS.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>
        {hasFilter ? (
          <button
            type="button"
            className="text-sm text-accent hover:underline"
            onClick={() => setFilters({ topic: '', person: '', mood: '' })}
          >
            clear filters
          </button>
        ) : null}
      </div>

      {!sessions ? (
        <p className="text-muted">Loading your timeline…</p>
      ) : sessions.length === 0 ? (
        <p className="text-muted">{hasFilter ? 'No sessions match these filters.' : 'No sessions yet — start one above.'}</p>
      ) : (
        groupByDay(dayJump ? sessions.filter((s) => s.journalingDay === dayJump) : sessions).map(([day, items]) => (
          <section key={day} className="space-y-2">
            <h2 className="text-sm font-medium text-muted">{formatDay(day)}</h2>
            <ul className="space-y-2">
              {items.map((s) => (
                <li key={s.id}>
                  <Link
                    to="/sessions/$sessionId"
                    params={{ sessionId: s.id }}
                    className="block rounded-lg border border-border bg-surface-2 p-3 hover:bg-surface-3"
                  >
                    <span className="text-content">{s.preview || <em className="text-muted">Empty session</em>}</span>
                    <span className="mt-1 block text-xs text-muted">{new Date(s.createdAt).toLocaleTimeString()}</span>
                    <MetadataChips item={s} />
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </div>
  )
}

function MetadataChips({ item }: { item: SessionListItem }) {
  const chips: string[] = [
    ...item.topics.map((t) => `#${t}`),
    ...item.people.map((p) => `@${p}`),
    ...(item.mood ? [item.mood.key === 'Custom' ? (item.mood.customValue ?? 'Custom') : item.mood.key] : []),
  ]
  if (chips.length === 0) return null
  return (
    <span className="mt-2 flex flex-wrap gap-1">
      {chips.map((c, i) => (
        <span key={`${c}-${i}`} className="rounded-full bg-surface-3 px-2 py-0.5 text-xs text-muted">
          {c}
        </span>
      ))}
    </span>
  )
}

function TimeZonePicker() {
  const { data: settings } = useSettings()
  const updateSettings = useUpdateSettings()

  // Default from the browser on first run (no stored zone yet).
  useEffect(() => {
    if (settings && settings.timeZoneId === null) {
      const browser = Intl.DateTimeFormat().resolvedOptions().timeZone
      if (browser) updateSettings.mutate({ ...settings, timeZoneId: browser })
    }
  }, [settings, updateSettings])

  if (!settings) return null

  const current = settings.timeZoneId ?? 'UTC'
  const zones = COMMON_ZONES.includes(current) ? COMMON_ZONES : [current, ...COMMON_ZONES]

  return (
    <label className="flex items-center gap-2 text-sm text-muted">
      Timezone
      <select
        value={current}
        onChange={(e) => updateSettings.mutate({ ...settings, timeZoneId: e.target.value })}
        className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
      >
        {zones.map((zone) => (
          <option key={zone} value={zone}>
            {zone}
          </option>
        ))}
      </select>
    </label>
  )
}

/** Per-user geo opt-in: when on, starting a session asks the browser for a single point (issue 0015). */
function LocationToggle() {
  const { data: settings } = useSettings()
  const updateSettings = useUpdateSettings()

  if (!settings) return null

  return (
    <label className="flex items-center gap-2 text-sm text-muted">
      <input
        type="checkbox"
        checked={settings.locationCaptureEnabled}
        onChange={(e) => updateSettings.mutate({ ...settings, locationCaptureEnabled: e.target.checked })}
        className="rounded border-border accent-accent"
      />
      Capture location on new sessions
    </label>
  )
}

function groupByDay(items: SessionListItem[]): Array<[string, SessionListItem[]]> {
  const groups: Array<[string, SessionListItem[]]> = []
  for (const item of items) {
    const last = groups[groups.length - 1]
    if (last && last[0] === item.journalingDay) last[1].push(item)
    else groups.push([item.journalingDay, [item]])
  }
  return groups
}

function formatDay(day: string): string {
  const [y, m, d] = day.split('-').map(Number)
  return new Date(y!, m! - 1, d!).toLocaleDateString(undefined, {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}
