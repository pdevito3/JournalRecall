// Query-key factory for the auth feature (FE-031). `me` and `config` are independent cache roots today
// (the current-session payload vs. the public instance config), so their literal values are preserved
// exactly — `me` stays `['me']`, `config` stays `['auth', 'config']` — keeping cache coverage unchanged.
export const authKeys = {
  all: ['auth'] as const,
  me: ['me'] as const,
  config: ['auth', 'config'] as const,
}
