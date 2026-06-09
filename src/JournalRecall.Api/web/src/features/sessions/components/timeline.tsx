import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, getRouteApi } from '@tanstack/react-router'
import { HugeiconsIcon } from '@hugeicons/react'
import { buildSessionFilter, useSessionList, type TimelineSearch } from '@/features/sessions/useSessions'
import { ACTIVITY_ICONS, KNOWN_ACTIVITIES, KNOWN_MOODS, type SessionListItem } from '@/features/sessions/api'

const route = getRouteApi('/')

/**
 * Settings access is injected by the route (which owns the settings feature) so the Session
 * vertical never reaches into the settings vertical directly (FE-017). `settings` is undefined
 * while loading; the picker/toggle render nothing until it arrives. `onUpdateSettings` persists
 * a full settings object, matching the settings PUT contract.
 */
export interface TimelineSettings {
  timeZoneId: string | null
  locationCaptureEnabled: boolean
  requirePeopleTagApproval: boolean
}

export interface TimelineProps {
  settings: TimelineSettings | undefined
  onUpdateSettings: (settings: TimelineSettings) => void
}

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

export function Timeline({ settings, onUpdateSettings }: TimelineProps) {
  const [dayJump, setDayJump] = useState('') // YYYY-MM-DD, or '' for all
  // Topic/Mood/Activity live in the URL (FE-009) so a filtered view is shareable and survives refresh.
  const { topic, mood, activity } = route.useSearch()
  const navigate = route.useNavigate()
  const setFilters = (next: Partial<TimelineSearch>) =>
    navigate({ search: (prev) => ({ ...prev, ...next }) })

  // Build a QueryKit filter string from the metadata controls (server-side filtering). Mood and Activity
  // are separate params (a JSON collection / a complex-type scalar, both outside QueryKit).
  const filter = useMemo(() => buildSessionFilter({ topic, mood, activity }), [topic, mood, activity])

  const { data: sessions } = useSessionList(filter, mood || undefined, activity || undefined)
  const hasFilter = Boolean(filter) || Boolean(mood) || Boolean(activity)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <TimeZonePicker settings={settings} onUpdateSettings={onUpdateSettings} />
        <LocationToggle settings={settings} onUpdateSettings={onUpdateSettings} />
        <PeopleTagApprovalToggle settings={settings} onUpdateSettings={onUpdateSettings} />
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
        <select
          value={activity}
          onChange={(e) => setFilters({ activity: e.target.value as TimelineSearch['activity'] })}
          className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <option value="">Any activity</option>
          {KNOWN_ACTIVITIES.map((a) => (
            <option key={a} value={a}>
              {a}
            </option>
          ))}
        </select>
        {hasFilter ? (
          <button
            type="button"
            className="text-sm text-accent hover:underline"
            onClick={() => setFilters({ topic: '', mood: '', activity: '' })}
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
  // The single Activity shows first when set (not the 'None' zero value), with its Hugeicons glyph (PRD-0007).
  const hasActivity = Boolean(item.activity) && item.activity !== 'None'
  const activityIcon = hasActivity ? ACTIVITY_ICONS[item.activity] : undefined
  const chips: string[] = [...item.topics.map((t) => `#${t}`), ...item.people.map((p) => `@${p}`), ...item.moods]
  if (!hasActivity && chips.length === 0) return null
  return (
    <span className="mt-2 flex flex-wrap gap-1">
      {hasActivity ? (
        <span className="inline-flex items-center gap-1 rounded-full bg-surface-3 px-2 py-0.5 text-xs text-muted">
          {activityIcon ? <HugeiconsIcon icon={activityIcon} size={12} /> : null}
          {item.activity}
        </span>
      ) : null}
      {chips.map((c, i) => (
        <span key={`${c}-${i}`} className="rounded-full bg-surface-3 px-2 py-0.5 text-xs text-muted">
          {c}
        </span>
      ))}
    </span>
  )
}

function TimeZonePicker({ settings, onUpdateSettings }: TimelineProps) {
  // One-shot guard: the browser-default persist must fire at most once, never as a
  // surprise re-write on re-render/StrictMode remount.
  const seededDefault = useRef(false)

  // Derive the effective zone at render: stored value, else the browser zone, else UTC.
  // This is what the picker shows even before any default is persisted.
  const browser = Intl.DateTimeFormat().resolvedOptions().timeZone
  const current = settings?.timeZoneId ?? browser ?? 'UTC'

  // Server-side journaling-day bucketing reads the stored value, so seed the derived
  // browser default exactly once when none is stored yet. The ref guard keeps this a
  // single one-shot — it never re-fires on re-render or a StrictMode remount.
  useEffect(() => {
    if (settings && settings.timeZoneId === null && browser && !seededDefault.current) {
      seededDefault.current = true
      onUpdateSettings({ ...settings, timeZoneId: browser })
    }
  }, [settings, browser, onUpdateSettings])

  if (!settings) return null

  const zones = COMMON_ZONES.includes(current) ? COMMON_ZONES : [current, ...COMMON_ZONES]

  return (
    <label className="flex items-center gap-2 text-sm text-muted">
      Timezone
      <select
        value={current}
        onChange={(e) => onUpdateSettings({ ...settings, timeZoneId: e.target.value })}
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
function LocationToggle({ settings, onUpdateSettings }: TimelineProps) {
  if (!settings) return null

  return (
    <label className="flex items-center gap-2 text-sm text-muted">
      <input
        type="checkbox"
        checked={settings.locationCaptureEnabled}
        onChange={(e) => onUpdateSettings({ ...settings, locationCaptureEnabled: e.target.checked })}
        className="rounded border-border accent-accent"
      />
      Capture location on new sessions
    </label>
  )
}

/**
 * Per-user gate on AI People-tagging (PRD-0006, RICH-009): on by default, Cleanup proposes People tags for
 * per-Person approval. Turning it off lets a run tag resolved People inline automatically.
 */
function PeopleTagApprovalToggle({ settings, onUpdateSettings }: TimelineProps) {
  if (!settings) return null

  return (
    <label className="flex items-center gap-2 text-sm text-muted">
      <input
        type="checkbox"
        checked={settings.requirePeopleTagApproval}
        onChange={(e) => onUpdateSettings({ ...settings, requirePeopleTagApproval: e.target.checked })}
        className="rounded border-border accent-accent"
      />
      Review AI people tags before applying
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
