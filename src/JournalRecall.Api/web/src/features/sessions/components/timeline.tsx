import { useEffect, useState } from 'react'
import { Link } from '@tanstack/react-router'
import { useSettings, useUpdateSettings } from '@/features/settings/useSettings'
import { useSessionList } from '@/features/sessions/useSessions'
import type { SessionListItem } from '@/features/sessions/api'

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
  const { data: sessions } = useSessionList()
  const [dayJump, setDayJump] = useState('') // YYYY-MM-DD, or '' for all

  if (!sessions) return <p className="text-muted">Loading your timeline…</p>
  if (sessions.length === 0) return <p className="text-muted">No sessions yet — start one above.</p>

  const visible = dayJump ? sessions.filter((s) => s.journalingDay === dayJump) : sessions
  const groups = groupByDay(visible)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <TimeZonePicker />
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

      {groups.map(([day, items]) => (
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
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}

function TimeZonePicker() {
  const { data: settings } = useSettings()
  const updateSettings = useUpdateSettings()

  // Default from the browser on first run (no stored zone yet).
  useEffect(() => {
    if (settings && settings.timeZoneId === null) {
      const browser = Intl.DateTimeFormat().resolvedOptions().timeZone
      if (browser) updateSettings.mutate({ timeZoneId: browser })
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
        onChange={(e) => updateSettings.mutate({ timeZoneId: e.target.value })}
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
