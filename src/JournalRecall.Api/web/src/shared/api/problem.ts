/**
 * The structured server-error seam (PRD-0004 / ADR-0007). The API client used to flatten an ASP.NET
 * `ValidationProblemDetails` body into a single joined string before any form could see the per-field
 * `errors` dict. Instead, `problemError` parses the body into a {@link ProblemError} whose `.problem`
 * is the full ProblemDetails object (so `applyServerErrors` can map field errors) and whose `.message`
 * keeps the same flattened fallback string the old `problem()` helper produced — so any existing code
 * reading `error.message` keeps working unchanged.
 *
 * Designed against the current ASP.NET ProblemDetails shape and forward-compatible with the upcoming
 * server-side ProblemDetails standardization.
 */

export interface ProblemDetails {
  type?: string
  title?: string
  detail?: string
  status?: number
  /** Per-field validation errors, server field-name → messages (the part the old helper destroyed). */
  errors?: Record<string, string[]>
  [key: string]: unknown
}

export class ProblemError extends Error {
  /** The parsed ProblemDetails, or null when the body wasn't a usable problem object. */
  readonly problem: ProblemDetails | null

  constructor(message: string, problem: ProblemDetails | null = null) {
    super(message)
    this.name = 'ProblemError'
    this.problem = problem
  }
}

/**
 * Parse a failed `Response` into a `ProblemError`: `.problem` carries the parsed ProblemDetails (or
 * null for an opaque/non-JSON body), `.message` is the flattened fallback (joined field errors, else
 * `title`, else `detail`, else the supplied `fallback`).
 */
export async function problemError(res: Response, fallback: string): Promise<ProblemError> {
  let problem: ProblemDetails | null = null
  try {
    const body = await res.json()
    if (body && typeof body === 'object') problem = body as ProblemDetails
  } catch {
    // Opaque or non-JSON body — leave problem null and fall back to the supplied message.
  }
  return new ProblemError(flatten(problem, fallback), problem)
}

function flatten(problem: ProblemDetails | null, fallback: string): string {
  if (problem) {
    if (problem.errors) {
      const joined = Object.values(problem.errors).flat().join(' ')
      if (joined.length > 0) return joined
    }
    if (typeof problem.title === 'string' && problem.title.length > 0) return problem.title
    if (typeof problem.detail === 'string' && problem.detail.length > 0) return problem.detail
  }
  return fallback
}
