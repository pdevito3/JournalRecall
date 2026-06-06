# PRD 0005 — Frontend Pattern Hardening (Router+Query, vertical boundaries, selectors, derived state, compound forms, e2e)

**Status:** ready-for-agent · **Type:** AFK · **Delivery:** incremental — keystone first, then six
independent work-streams · **Introduces:** ADR-0008 (compound form components, **supersedes**
[ADR-0007](../adr/0007-forms-on-tanstack-react-form-zod.md)) · **Hardens:**
[ADR-0001](../adr/0001-vite-spa-embedded-in-dotnet.md) (SPA),
[ADR-0006](../adr/0006-three-layer-test-suite-user-isolated-sqlite.md) (tests).

> Domain language per [`CONTEXT.md`](../../CONTEXT.md): the surfaces touched here render and edit
> **Session**s (**Raw** / **Cleaned** / **Draft** text, **Synopsis**, **Cleanup status**,
> **Revision** drill-downs), **Metadata** (**Topic** / **Person** / **Mood**, with **Suggestion**
> provenance), period **Summaries**, **Corrections**, and the **Admin** surface (users, AI
> provider/model). Routing respects the **Privacy invariant** (all journal data is strictly per
> **User**) and the **Journaling day** projection. This PRD changes *how the web client is wired*,
> not what it does — no domain behavior, API contract, or schema changes.

## Problem Statement

As a developer on the JournalRecall web client, the frontend already follows good instincts —
vertical feature folders, a single `QueryClient` in router context, the access gate in the root
`beforeLoad`, a clean React-Query read/mutate layer, and a fully accessible react-aria UI — but the
patterns are enforced by discipline alone and have drifted in several concrete, load-bearing ways:

- **Server-state query definitions are duplicated and can silently drift.** The `me` and
  auth-config queries are spelled out once in the auth feature's hooks and *again* by hand in the
  root route's `beforeLoad`. There is no `queryOptions()` factory anywhere, so a key/`staleTime`
  change in one place is a stale gate or a double-fetch in the other.
- **Feature boundaries are convention-only.** There is no ESLint config in the web project at all,
  no per-feature public-API barrel, and at least one real cross-feature reach (the **Session**
  timeline imports the **settings** feature's hook directly). Nothing prevents the next violation.
- **A genuine server→local state-mirroring bug exists.** The **Session** detail editor copies
  server **Raw** **Draft** text into local state via `useEffect` behind a per-instance "hydrated"
  latch. Because TanStack Router reuses the component across `/sessions/A → /sessions/B`, the editor
  can show one Session's text while displaying another's identity.
- **Routes carry too much logic.** The Session detail route is ~440 lines of editor state machine;
  the **Admin** route ~300. Routes should be thin shells delegating to feature components — the
  pattern exists (the timeline, the index route) but is applied inconsistently.
- **Every route waterfalls mount→fetch.** No leaf route uses a `loader`; `defaultPreload: 'intent'`
  warms only the auth queries, never route data. View state that belongs in the URL
  (**Summary** period/date, timeline **Topic**/**Person**/**Mood** filters) lives in `useState`, so
  it isn't shareable, bookmarkable, or back-button-able.
- **The Admin role gate is imperative and late.** The Admin route renders, then bails inside the
  component with a "no access" message, flashing the wrong page instead of redirecting at navigation
  time.
- **Derived server values are recomputed ad hoc.** The `roles.includes('Admin')` rule is duplicated
  across the root nav and the Admin route; no query uses `select`, so components subscribe to whole
  payloads when they need a slice.
- **The shared form layer erases its types.** Per ADR-0007 the form instance is prop-drilled and
  fields cross into the shared components as `AnyFieldApi` / `AnyFormApi` (`as string` casts),
  so the shared layer carries no knowledge of a form's schema keys or value types.
- **E2E guidance teaches the wrong selectors.** The dev-runbook's example **dev-browser** scripts
  use brittle CSS (`input[name=…]`, `span.text-red-400`, `p[role="alert"]`) and network-idle waits —
  exactly what Playwright best practices warn against — even though the accessible UI makes
  role/label locators free. There is no committed e2e helper, so every flow re-types login/setup and
  rots independently.

## Solution

As a developer, I get the same well-instinct'd architecture **with its patterns made explicit,
enforced, and de-duplicated**, delivered as six mostly-independent work-streams behind one keystone:

- **Keystone — `queryOptions()` factories.** One factory per query, per feature, consumed by hooks,
  route loaders, and `select`-based derived hooks alike. The duplicated `me` / auth-config
  definitions collapse to a single source of truth. This unblocks the loader and selector work.
- **Router + Query integration.** Add `ensureQueryData` loaders to the data-heavy routes (kill the
  waterfalls), move the **Admin** role gate into `beforeLoad` (redirect, no flash), and make the
  timeline filters and **Summary** period/date URL state via `validateSearch` + zod + `loaderDeps`.
- **Vertical boundaries.** Introduce ESLint with an import-boundary rule encoding
  `routes → features → shared` (no feature→feature), add per-feature public-API barrels, resolve the
  Session→settings cross-feature edge, and extract heavy route logic into feature components so
  routes become thin shells.
- **Deriving client state from server state.** Replace the **Session** editor's `useEffect`
  hydration with `key`-based remount on Session identity (fixing the cross-Session stale-text bug),
  key the **Metadata** and AI-provider edit forms on their entity identity so refetched server values
  re-seed, and prefer render-time derivation over effects.
- **React Query selectors.** Add stable, module-level auth selectors (`useIsAdmin`, `useAuthRoles`)
  and an optional `select` parameter on the shared `me` hook, de-duplicating the Admin-role rule.
- **Type-safe compound form components.** Supersede ADR-0007: introduce a `<Form>` compound parent
  backed by a type-safe context + a `useFormContext()` hook that throws when misused, expose
  `Form.Field` / `Form.Submit` / `Form.Errors`, and tie field names to schema keys via a
  `createForm<Schema>()` factory — removing the form prop-drilling and the `Any*` type erasure.
- **E2E hardening.** Rewrite the dev-runbook to teach `getByRole`/`getByLabel` + web-first
  auto-retrying assertions, and add a committed **dev-browser** helper module (`login`,
  `completeSetup`, base-URL resolution) as the single source of truth for e2e flows.

Each topic is tiered **MUST / SHOULD / NICE**; open questions are recorded as explicit decision
points in Further Notes.

## User Stories

### Keystone — queryOptions factories
1. As a developer, I want one `queryOptions()` factory per query per feature, so that the queryKey,
   queryFn, and `staleTime` for a query are defined exactly once.
2. As a developer, I want the root `beforeLoad` access gate to consume the same auth and `me`
   factories as the hooks, so that the gate and the components can never disagree on the cache key.
3. As a developer, I want loaders, hooks, and `select`-based hooks to all build on these factories,
   so that priming the cache in a loader and reading it in a component are guaranteed consistent.

### Router + Query integration
4. As a user, I want the **Session** detail, **Admin**, and **Corrections** screens to start
   fetching during navigation rather than after mount, so that pages feel instant and don't waterfall.
5. As a user, I want hover/intent preloading to warm route data (not just the auth queries), so that
   navigating to a screen I'm pointing at is already loading.
6. As a non-admin **Member**, I want to be redirected away from the **Admin** route before it renders,
   so that I never see a flash of the admin page followed by a "no access" message.
7. As a user, I want the timeline's **Topic** / **Person** / **Mood** filters in the URL, so that I
   can share, bookmark, refresh, and back-button a filtered view of my journal.
8. As a user, I want the **Summary** period (`Day | Week | Month | Quarter | Year`) and anchor date in
   the URL, so that a specific period roll-up is a shareable, refresh-surviving link.
9. As a developer, I want filter/period URL state validated by a zod schema via `validateSearch`, so
   that malformed search params are normalized to defaults instead of crashing a route.
10. As a developer, I want `loaderDeps` to select which search params feed a loader, so that the
    loader re-runs only when the inputs that affect the query change.
11. As a user, I want consistent loading and error UI across routes, so that every screen's pending
    and failure states look and behave the same.
12. As a developer, I want server state to stay owned by React Query (loaders prime the cache, never
    `useLoaderData`), so that focus/reconnect refetch, dedup, and GC keep working on loaded routes.

### Vertical codebase / boundaries
13. As a developer, I want an ESLint import-boundary rule encoding `routes → features → shared`, so
    that an accidental feature→feature or shared→feature import fails lint instead of merging.
14. As a developer, I want each feature to expose a public-API barrel, so that consumers import a
    feature's intended surface and I can refactor its internals freely.
15. As a developer, I want the **Session** timeline's dependency on the **settings** feature resolved
    (promoted to shared, lifted to the route as props, or an explicit allowed edge), so that there is
    no direct feature→feature reach.
16. As a developer, I want routes to be thin composition shells, so that the editor state machine and
    admin logic live in testable feature components rather than 300–440-line route files.
17. As a developer, I want a documented criterion for promoting code to `shared` (used by ≥2 features
    and domain-agnostic, else promote to its own vertical), so that `shared` doesn't accrete
    single-use code.
18. As a developer, I want a `lint` script wired into the project and CI, so that boundary and style
    rules are actually enforced, not aspirational.

### Deriving client state from server state
19. As a user editing a **Session**, I want the **Raw** **Draft** editor to always show the correct
    Session's text when I navigate between Sessions, so that I never see one Session's words under
    another's heading.
20. As a developer, I want the Session editor to reset via `key` on Session identity rather than a
    `useEffect` + "hydrated" latch, so that local edit state is rebuilt fresh per Session by React.
21. As a user, I want the **Metadata** editor and the **Admin** AI-provider form to re-seed when the
    underlying server entity changes (e.g. after an accepted **Suggestion** or a **Cleanup** re-run),
    so that I'm never editing stale server values.
22. As a user editing the **Cleaned** copy, I want my unsaved edits preserved until a save point, and
    a server regeneration to re-seed the editor, so that a **Cleanup** re-run doesn't silently clobber
    or strand my hand-edits.
23. As a developer, I want a lint/review guardrail flagging `useEffect` that calls `setState` from
    query data, so that the derive-don't-sync rule survives future features.
24. As a user, I want the timezone default derived at render rather than auto-persisted by a render
    effect, so that opening the timeline doesn't fire a surprise settings write (or fire it twice).

### React Query selectors
25. As a developer, I want a single `isAdmin` selector backing the nav and the **Admin** gate, so
    that the Admin-role rule is defined once and can't drift between call sites.
26. As a developer, I want stable, module-level selector functions, so that they're reusable and
    don't re-run from being recreated each render.
27. As a developer, I want the shared `me` hook to accept an optional `select`, so that a component
    can subscribe to just the slice it needs (e.g. `username`, `mustChangePassword`, roles) and
    re-render only when that slice changes.
28. As a developer, I want selectors applied only where a real slice/over-render exists (not on
    hooks that legitimately render whole payloads like the timeline list), so that we don't add
    premature optimization.

### Type-safe compound form components (supersedes ADR-0007)
29. As a developer, I want a `<Form>` compound component that provides the form instance via a
    type-safe context, so that sub-components read the form from context instead of every call site
    prop-drilling it.
30. As a developer, I want a `useFormContext()` hook that throws a clear, developer-readable error
    when a `Form.*` sub-component is used outside `<Form>`, so that misuse fails loudly at render
    rather than as a null-property crash.
31. As a developer, I want `Form.Field` / `Form.Submit` / `Form.Errors` exposed as static
    sub-components, so that a form composes from one discoverable, importable surface.
32. As a developer, I want a `createForm<Schema>()` factory that returns components whose field
    `name` is the schema's key union and whose render-prop field carries that key's value type, so
    that field/value-type mismatches are caught at compile time instead of erased to `any`.
33. As a developer, I want `applyServerErrors` strengthened against the schema's static key union,
    so that the runtime "is this a known field" check becomes a typed mapping.
34. As a developer, I want **Mood**'s `Custom`-member conditional field and the comma-string →
    array split (**Topic**/**Person**/**Correction** mishearings) to keep working under the compound
    API, so that superseding ADR-0007 changes the wiring, not the validation behavior.
35. As a developer, I want `SelectField` options to remain a prop (not a `<Select.Option>` compound),
    so that data-mapped option lists aren't forced into a compound shape the article warns against.

### E2E (dev-browser) hardening
36. As a developer, I want dev-browser e2e scripts to locate elements by role/label/text
    (`getByRole`, `getByLabel`, `getByRole('alert')`), so that scripts survive class/DOM changes and
    assert what the user sees.
37. As a developer, I want web-first auto-retrying assertions instead of `sleep`/`networkidle`, so
    that scripts aren't flaky against background React-Query refetches.
38. As a developer, I want a committed dev-browser helper module (`login`, `completeSetup`,
    base-URL/port resolution), so that login/setup logic is written once and flows don't diverge.
39. As a developer, I want each e2e flow to declare its precondition (fresh DB → setup, or seeded →
    login) and handle the first-run **setup** gate, so that re-runs don't fail on "username taken" or
    land on the wrong page.
40. As a developer, I want the dev-runbook rewritten to teach these conventions and demote CSS
    selectors to a last-resort fallback, so that the guidance stops teaching the anti-patterns.
41. As a developer, I want third-party surfaces (geolocation for **Location**, AI-provider responses)
    stubbed or skipped in e2e, so that flows don't assert on systems we don't control.

## Implementation Decisions

**Delivery & sequencing.** Incremental, not big-bang. The **queryOptions factories** land first as
the keystone (the Router+Query loaders and the selectors both build on them). The remaining five
topics are independent and can ship in any order. No domain behavior, API contract, or persistence
change anywhere in this PRD — it is purely client wiring plus the dev-runbook.

**Keystone — queryOptions factories (MUST).** Each feature gains query-option factories (one per
query, parameterized where the key is). Hooks call `useQuery(factory(args))`; the root `beforeLoad`
calls `ensureQueryData(factory())`; selector hooks spread the factory and add `select`. The auth and
`me` definitions currently hand-duplicated in the root route are deleted in favor of the factory.
*Deep module, simple interface (args → options), rarely changes.*

**Router + Query.**
- *MUST* — Move the **Admin** role gate into the Admin route's `beforeLoad`: `ensureQueryData` the
  `me` factory and `throw redirect` to the journal for non-admins; delete the in-component check.
- *SHOULD* — Add `ensureQueryData` loaders to the **Session** detail, **Admin**, and **Corrections**
  routes; secondary lists on the Session screen (**Revision** streams) use non-awaited prefetch so
  they stream rather than block first paint. Components keep reading via `useQuery` (never
  `useLoaderData`).
- *SHOULD* — Move timeline filters and **Summary** period/date into URL search state via
  `validateSearch` (zod) + `loaderDeps`; the loader keys its query off the validated deps.
- *SHOULD* — Configure router-level default pending/error components and remove the repeated
  per-component `isLoading`/`isError` branches on loader-backed routes.
- *NICE* — Set `defaultPreloadStaleTime: 0` so Query owns preload cache lifetime under the existing
  `defaultPreload: 'intent'`.
- *Decision points:* Suspense (`useSuspenseQuery`) vs `useQuery`+branches on loaded routes; how
  aggressively to add loaders (Session detail clearly benefits; light routes may not); whether the
  root `beforeLoad` funnel shrinks as per-route guards appear; keep the **Cleanup** event stream
  outside the cache (it is intentionally local, SSE-style state, not a query).

**Vertical boundaries.**
- *MUST* — Introduce a flat ESLint config with an import-boundary rule (`eslint-plugin-boundaries`
  or `import/no-restricted-paths`) encoding `routes → features (+ own feature only) → shared →
  shared`; wire a `lint` script into the project and CI.
- *MUST* — Resolve the **Session**→**settings** cross-feature import per the decision in Further
  Notes (promote settings access to shared, pass settings into the timeline as props from the route,
  or sanction it as an allowed composition edge).
- *SHOULD* — Add per-feature public-API barrels and switch route imports to them; extract the heavy
  logic out of the Session detail, **Admin**, **Summaries**, and **Corrections** routes into feature
  components, leaving routes as `createFileRoute` + render shells.
- *NICE* — Document the "promote to shared" criterion and codify file-naming conventions
  (kebab-case files, `useX` hooks, `index` barrels) in the lint config / contributing notes; split
  the large session feature module into types/constants/api behind the barrel.

**Deriving client state from server state.**
- *MUST* — Reset the **Session** editor by `key` on Session identity at the route boundary and
  delete the `useEffect` + "hydrated" latch; seed the **Raw** **Draft** local state directly from
  server data (valid once the component is guaranteed fresh per Session).
- *SHOULD* — Key the **Metadata** editor and the **Admin** AI-provider form on their entity identity
  (plus a change-token where one exists) so refetched server values re-seed instead of going stale;
  simplify the **Cleaned** editor's manual ref/effect reconciler to a `key` on Session identity +
  the cleaned **Revision** change-token, preserving the "local unsaved edits win until a save point"
  intent.
- *SHOULD* — Derive the timezone default at render and persist only on explicit user action (or a
  one-shot guard), rather than auto-mutating settings from a render effect.
- *NICE* — Add the "`useEffect` that `setState`s from query data is a smell — derive or `key`" review
  guardrail.
- *Decision points:* confirm Router reuses the Session component across param changes (the `key`
  fixes it regardless); identify the canonical change-token on the Session DTO for keying the Cleaned
  editor; confirm the cross-tab "what wins on a concurrent server change with unsaved edits" policy.

**React Query selectors.**
- *MUST* — Add stable, module-level auth selectors and `useIsAdmin` / `useAuthRoles` derived hooks
  built on the `me` factory; replace the duplicated `roles.includes('Admin')` logic in the nav and
  the Admin gate.
- *SHOULD* — Give the shared `me` hook an optional `select` parameter (factory + `select`), with the
  derived hooks implemented on top of it.
- *NICE* — An auth-config `select` slice for `selfRegistrationEnabled` / `needsSetup`; a single-item
  selector over the session list — implemented only when a concrete consumer needs it.
- *Explicitly not done:* no memoized/`fast-memoize` selector wrappers (no expensive transforms
  exist), and no `select` on hooks that legitimately render whole payloads (timeline list, Session
  detail).

**Type-safe compound form components — introduces ADR-0008, supersedes ADR-0007.** This reverses
ADR-0007's "explicit `field` prop, no `createFormHook`/context" decision; a new **ADR-0008** records
the supersession and its rationale (the shared layer's `Any*` type erasure and form prop-drilling
became the cost the explicit-prop choice was meant to avoid).
- *SHOULD* — A type-safe form **context** (`createContext` defaulting to a missing sentinel) and a
  `useFormContext()` hook that throws a clear error outside `<Form>`. The `FormShell` chrome becomes
  the `<Form>` parent providing the form; `Form.Submit` and `Form.Errors` read context and are
  attached as static sub-components, centralizing the duplicated error-message rendering.
- *SHOULD* — A `createForm<Schema>()` factory returning a `Field` whose `name` is the schema key
  union and whose render-prop field carries the key's value type, restoring schema-key typing the
  shared layer currently erases; strengthen `applyServerErrors` against that static key union.
- *Invariants preserved:* per-form `useForm` + colocated zod schema + existing TanStack Query
  mutations are unchanged; **Mood** `Custom` conditional field and comma-string→array split behavior
  are unchanged; `SelectField` options stay a prop, not a compound. Register and **setup** remain
  separate forms sharing fragments.
- *Decision point:* build our own thin `createForm` factory vs. adopt TanStack's `createFormHook`
  (ADR-0008 must pick and justify); how far to push generics for 2–4-field forms.

**E2E (dev-browser).**
- *MUST* — Replace CSS selectors with role/label locators and `sleep`/`networkidle` with web-first
  auto-retrying assertions in all guidance and scripts; rewrite the dev-runbook's form-selector
  section accordingly.
- *MUST* — Add a committed dev-browser helper module: `completeSetup` (handles the first-run
  **setup** gate), `login`, and base-URL/port resolution as the single source of truth flows copy
  from. Each flow declares a precondition (fresh DB → setup, or seeded → unique-per-run identity).
- *SHOULD* — Parameterize the base URL/port; use `exact` label matching on the multi-password
  setup/register/change-password forms; scope locators to a region where roles repeat; stub/skip
  geolocation (**Location**) and AI-provider surfaces.
- *NICE* — A `waitFor` polling shim if the sandbox lacks Playwright's `expect`; a small catalog of
  named reference flows; screenshot-plus-`role=alert`-text capture on failure.
- *Decision points:* whether the dev-browser QuickJS sandbox exposes Playwright's `expect` matchers
  (a 2-minute spike); where e2e helpers/scripts live and whether they're committed; the canonical
  "reset to known state" command (delete the per-worktree SQLite DB vs. unique-per-run identities);
  whether e2e eventually graduates to the real Playwright test runner (principles port unchanged).

## Testing Decisions

A good test here asserts **external behavior**, not implementation: given inputs/props, assert the
value or rendered output a consumer observes — never react-form/router internal state shape. Tests
must survive a refactor of the wiring. Coverage concentrates where a bug is highest-leverage, and
**four modules get tests written**; everything else is verified by `lint`, by running the app, or by
a dev-browser flow per house style.

- **Pure auth selectors** — unit-test the module-level selector functions (`isAdmin`, roles) against
  representative `me` payloads (admin, member, null), and assert the derived hooks expose the
  selected slice. Pure functions, cheap, high-value; mirrors the existing pure-zod fragment tests.
- **`validateSearch` zod schemas** — unit-test the timeline and **Summary** URL search schemas:
  valid params parse, invalid/missing params fall back to defaults, the inferred type matches. Direct
  prior art: the existing shared form-schema unit tests.
- **`beforeLoad` Admin role gate** — a functional/integration test that a non-**Admin** **Member** is
  redirected away from the Admin route and an Admin is admitted, exercised through the existing
  three-layer harness ([ADR-0006](../adr/0006-three-layer-test-suite-user-isolated-sqlite.md);
  Functional layer with the user bound at the DbContext, https base address).
- **Form context throwing-hook** — unit-test that `useFormContext()` throws a clear error when a
  `Form.*` sub-component renders outside `<Form>`, and returns the form within it.

Prior art and tooling: the web client uses Vitest 4 + Testing Library React 16 (already installed);
pure-schema and bound-field component tests already exist as models. Backend
integration/functional tests are unaffected (they exercise the API, not the client wiring) and must
stay green. Per-route happy-path component tests are **not** in scope beyond the four above; converted
routes and forms get a manual/dev-browser pass.

## Out of Scope

- **Any domain, API-contract, or persistence change.** This is client wiring plus the dev-runbook
  only. No endpoint, DTO, migration, or **Cleanup**/**Summary**/**Revision** behavior changes.
- **The autosave **Draft** save model.** The debounced per-keystroke **Raw**/**Cleaned** save stays
  as-is (it deliberately sits outside react-form's submit model and outside the query cache); only
  its *hydration* and *reset-on-navigation* are fixed.
- **The **Cleanup** event stream.** It remains intentionally local SSE-style component state, not a
  query; it does not get a `queryOptions` factory.
- **Memoized/`fast-memoize` selectors** and `select` on whole-payload hooks — explicitly avoided as
  premature.
- **Adopting the Playwright test runner / CI e2e.** The principles are adopted for dev-browser
  scripts now; the runner (trace viewer, sharding, isolation) is a separate future decision.
- **A schema-driven / auto-rendered form system.** Still rejected; the compound API is the ceiling
  of abstraction for this surface.
- **RBAC beyond the single `Admin` role.** The `isAdmin` selector encodes today's one-role rule;
  a role hierarchy is out of scope.

## Further Notes

- **The keystone unblocks two topics.** Land the `queryOptions()` factories first; the Router+Query
  loaders and the selector hooks both compose them, and the `me`/auth-config de-duplication is an
  immediate correctness win on the access gate.
- **The Session editor bug is the highest-priority single fix.** The `key`-on-identity remount (plus
  deleting the hydration effect) resolves a real cross-Session stale-text defect *and* re-seeds the
  Metadata/AI-provider/Cleaned forms, so several state stories collapse into one mechanical change.
- **ADR-0008 supersedes ADR-0007 deliberately.** The explicit-`field`-prop decision was sound at the
  time but its cost — shared-layer `Any*` type erasure and form prop-drilling — is now the thing the
  compound context + `createForm<Schema>()` factory removes. The ADR must choose own-factory vs.
  TanStack `createFormHook` and justify it.
- **Decision points carried into implementation** (each recorded above with its topic): is `settings`
  a feature or shared config (fixes the cross-feature edge); does Router reuse the Session component
  across params; what change-token keys the Cleaned editor; Suspense vs. branches on loaded routes;
  own form factory vs. `createFormHook`; does the dev-browser sandbox expose `expect`; where e2e
  helpers live and the canonical reset command; warn-first vs. hard-fail for the new lint boundary
  given existing 300–440-line routes.
- **Lint is greenfield.** There is no ESLint in the web project today, so the boundary rule arrives
  with the tooling; decide warn-first vs. error to avoid blocking CI on the pre-existing large routes
  until they're extracted.
- **The UI is already accessible**, so the e2e selector migration costs nothing — react-aria gives
  every form a labeled input, real buttons, `role="alert"` banners, and a per-page heading.
