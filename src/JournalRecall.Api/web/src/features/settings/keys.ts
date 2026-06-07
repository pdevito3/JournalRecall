// Query-key factory for the settings feature (FE-031). A single-key feature, kept as a factory for
// consistency so the literal lives in exactly one place.
export const settingsKeys = {
  all: ['settings'] as const,
}
