// Query-key factory for the admin feature (FE-031). One `all` root with a child per admin surface, so
// `invalidateQueries(adminKeys.all)` cascades across users / registration / ai-provider.
export const adminKeys = {
  all: ['admin'] as const,
  users: () => [...adminKeys.all, 'users'] as const,
  registration: () => [...adminKeys.all, 'registration'] as const,
  aiProvider: () => [...adminKeys.all, 'ai-provider'] as const,
}
