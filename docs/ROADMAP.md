# JournalRecall — Implementation Roadmap

Phased build-out of the design in [`../CONTEXT.md`](../CONTEXT.md) and [`adr/`](adr/). Ordered as a
**tracer bullet**: get a thin, genuinely usable slice running early (log in → write → re-read an
entry), then deepen one capability at a time. Each phase ends green (its tests pass) and is
independently valuable. ADR references show which decision each phase realizes.

A journal you can write in and read back is valuable **without** AI — so the AI library port and the
AI features come *after* the core writing loop is proven, not before.

| Phase | Theme | Realizes |
|------:|-------|----------|
| 0 | Scaffold & walking skeleton (.NET + Vite SPA, SQLite) | ADR-0001 |
| 1 | Identity & access (auth, tenancy, privacy) | ADR-0002 |
| 2 | Session core — write & re-read (the tracer bullet) | ADR-0003 |
| 3 | Port `JournalRecall.AI` agent framework | ADR-0004 |
| 4 | Cleanup pipeline + Corrections | ADR-0003, 0004 |
| 5 | Metadata (Topics, People, Mood, Suggestions) | — |
| 6 | Summaries (period roll-ups) | — |
| 7 | Geo, settings, admin & observability | ADR-0002 |
| 8 | Chat/RAG placeholder + containerized deployment | ADR-0001 |

---

## Phase 0 — Scaffold & walking skeleton
**Goal:** a compiling solution that boots and serves an end-to-end no-op path, single origin.
- `.slnx`; `JournalRecall.AI`, `JournalRecall.AI.EntityFrameworkCore`, `JournalRecall.Api`; test
  projects (mirror the PlateWise layout).
- Vite SPA scaffold under `JournalRecall.Api/web/` (TanStack Router client mode + Query + React Aria
  + Tailwind, **no** TanStack Start); builds to `wwwroot/app`; `.NET` serves `/app/*` with SPA
  fallback; dev: Vite proxies `/api` → .NET (one origin).
- `BaseEntity`, `JournalRecallDbContext` on **file-based SQLite**, first migration; Aspire AppHost
  for dev only.
- **Exit:** app boots; `/app` serves a placeholder page same-origin with `/api/health`; one pure
  test + one DI-wiring test green.

## Phase 1 — Identity & access
**Goal:** users authenticate; the privacy wall is enforced at the data layer. (ADR-0002)
- ASP.NET Core Identity: `User`, roles **Admin**/**Member**, password hashing, SQLite store.
- JWT minted on local login; delivered as a **strict HttpOnly cookie**; `JwtBearer`
  `OnMessageReceived` reads the cookie *or* the `Authorization` header (mobile-ready).
- **React auth routes** under `/app` (login/register/…) replacing the Identity Razor UI.
- `ICurrentUserService` from the validated principal; **HeimGuard** for admin permissions; **EF
  global query filter scoping every journal query to the current `UserId`** (Privacy invariant).
- **Exit:** register/login sets the cookie; protected `/api` requires auth; an admin-only endpoint
  403s for a member; a test proves a user cannot read another user's rows (global-filter test).

## Phase 2 — Session core (write & re-read)
**Goal:** the headline workflow works end-to-end, no AI. (ADR-0003)
- Rich **`Session`** aggregate: `Create`, autosaved **Draft**, append-only **Raw Revision** stream
  at save points; no lifecycle state. Vertical slice `Domain/Sessions/` — `Features/` (MediatR:
  `CreateSession`, `SaveDraft`, `GetSession`, `GetSessionList`), `Mappings/` (Mapster), `Dtos/`,
  `Services/`, `DomainEvents/`.
- **Journaling-day** derivation from a per-user **timezone** setting; UTC stored, day/week/month
  derived.
- React: **"start a new session" front-and-center**, type + autosave, session view, and a
  reverse-chron **timeline filtered via QueryKit** (current-state only); per-session **Revision
  history** drill-down.
- **Exit:** create → type → autosave → Revision-on-save; QueryKit-filtered list; registration-
  focused integration tests (`TestFixture`/`TestingServiceScope`, SQLite).

## Phase 3 — Port `JournalRecall.AI`
**Goal:** the full agent framework exists and is green, ready for AI features. (ADR-0004)
- Port `PlateWise.AI` wholesale (renamed): `Core/` (pure `AgentDefinition`/`AgentState`/`Decide`/
  `Authorize`/`OnToolError`), `Runtime/` (`IAgentRunner`, outer loop, Polly), capabilities
  (tools/resources/prompts/delegation), MCP interop, observability, conversation store + EF
  satellite, transports. `AddJournalRecallAgents(...)` DI entry; **BYO OpenAI-compatible**
  `IChatClient`.
- **Exit:** the ported test suite (pure core + `FakeChatClient` runner + tool schema + architecture
  boundary tests) green; a smoke agent runs to an `AgentOutcome`.

## Phase 4 — Cleanup pipeline + Corrections
**Goal:** AI produces a Cleaned copy and Synopsis without ever touching Raw. (ADR-0003, 0004)
- **Cleanup** as an agent/tool over `IChatClient`: emits **Cleaned** content (own Revision stream),
  **Synopsis**, structured **metadata Suggestions**. `Cleanup status` (`NotRun|Running|Clean|Stale|
  Failed`); **Stale** = latest Raw Revision newer than last successful Cleanup.
- **Corrections** per-user CRUD (canonical + mishearings); injected into the cleanup prompt as
  context; **hard-replace** flag for deterministic substitution. Applied to Cleaned only.
- **Re-run = warn-and-overwrite**, prior Cleaned Revision retained.
- React: manual **"Clean up with AI"** button, Raw/Cleaned side-by-side, **Stale** indicator,
  Corrections management page, **streamed progress** (SignalR/SSE off the agent event stream).
- **Exit:** Cleanup writes Cleaned + Synopsis, Raw byte-identical; a Correction is honored; re-run
  warns + keeps history; Stale lights up after a Raw edit.

## Phase 5 — Metadata
**Goal:** manual + AI-suggested metadata, human authoritative.
- **Topic**/**Person** as per-user data entities; **Mood** SmartEnum with a `Custom` value object;
  every tag carries **provenance** (`UserSet`/`AiSuggested`).
- **Suggestion** accept/reject flow; AI never overwrites user-set metadata.
- React: per-session metadata editing, suggestion accept/reject UI, filter the timeline by
  Topic/Person/Mood (QueryKit).
- **Exit:** manual + suggested metadata with provenance; accept/reject; filtering by each.

## Phase 6 — Summaries
**Goal:** period roll-ups on the summary page, on demand. (no scheduler in v1)
- **`Summary`** aggregate keyed by (user, period, date); **hybrid topology** — Day/Week from
  Sessions (Cleaned-if-present-else-Raw), Month←Days, Quarter←Months, Year←Quarters; **staleness
  propagates** up the chain.
- **On-demand generation** (lazy on view / explicit refresh) + as part of an AI-eval flow.
- React: summary page with Day/Week/Month/Quarter/Year, "generating…" + "Refresh" states.
- **Exit:** correct roll-up sources per level; staleness propagation test; on-demand generate/
  refresh; no background scheduler present.

## Phase 7 — Geo, settings, admin & observability
**Goal:** round out the per-user and admin surfaces; telemetry first-class. (ADR-0002)
- **Location**: per-user opt-in (default off) single geo-point captured at Session creation,
  declinable per session; coordinates only.
- Per-user **settings** (timezone, geo opt-in); **admin app-wide settings** (AI provider/model,
  later OIDC); **admin** user management + system health/telemetry pages (HeimGuard-gated).
- **Observability**: OpenTelemetry + Serilog wired as a first-class concern (mirror the template);
  content-capture opt-in + redaction for the AI lifecycle; optional browser RUM.
- **Exit:** geo captured only when opted in; admin manages users and views health; traces/logs flow
  for an end-to-end Cleanup.

## Phase 8 — Chat placeholder + deployment
**Goal:** ship the home-lab artifact. (ADR-0001)
- **Chat/RAG** page = "coming soon" placeholder only (no embeddings/vector store yet).
- **Single-container** Dockerfile (API serves `/api` + `/app`, SQLite `.db` on a **mounted
  volume**); `docker compose` for the home lab; data survives restart/upgrade.
- **Exit:** image builds and runs; journal data persists across a container restart on the volume;
  placeholder chat page visible.

---

### Deferred (post-v1, noted in ADRs / CONTEXT)
- **Nightly batch scheduler** (proactive Cleanup + pre-generated Summaries) — in-process when added.
- **External OIDC providers** — authentication-only federation; `Duende.AccessTokenManagement` only
  earns its place if we later call upstream APIs on the user's behalf (ADR-0002).
- **Mobile apps** (native dictation; possible **on-device LLM** for Cleanup/Summary) — the AI
  boundary is kept clean so this stays open (ADR-0004).
- **Chat/RAG for real** — embeddings + vector store on the ported agent runner.
- **Reverse-geocoded place labels**; **semantic search** over entries.

### Notes
- Phases 0–2 are the tracer bullet (auth → write → re-read) and deliver a usable journal with no AI.
- Phase 3 ports the library; Phases 4–6 are the AI features that build on it.
- Phase 7–8 finish the per-user/admin surfaces and produce the deployable container.
