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
