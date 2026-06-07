// Query-key factory for the corrections feature (FE-031). A single-key feature, kept as a factory for
// consistency so the literal lives in exactly one place.
export const correctionKeys = {
  all: ['corrections'] as const,
}
