// Sessions feature — runtime constants. Split out of `api.ts` per FE-023.
// Re-exported from `api.ts` so existing intra-feature imports keep resolving.

/** The app-defined known moods (mirrors the server's Mood.Known). */
export const KNOWN_MOODS = [
  'Joyful',
  'Content',
  'Calm',
  'Neutral',
  'Tired',
  'Anxious',
  'Sad',
  'Angry',
  'Excited',
  'Grateful',
] as const

/**
 * The app-defined known activities a User can pick, excluding the special 'None' zero value and the custom
 * free-text escape hatch (mirrors the server's Activity.KnownKeys minus None). PRD-0007.
 */
export const KNOWN_ACTIVITIES = [
  'Stationary',
  'Walking',
  'Eating',
  'Commuting',
  'Exercising',
  'Resting',
] as const

/** A recognizable glyph per known activity (and the 'None' zero value) — purely presentational (PRD-0007). */
export const ACTIVITY_ICONS: Record<string, string> = {
  None: '—',
  Stationary: '🛋️',
  Walking: '🚶',
  Eating: '🍽️',
  Commuting: '🚌',
  Exercising: '🏃',
  Resting: '😴',
}
