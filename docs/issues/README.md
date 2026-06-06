# Issues

In-repo issue tracker for JournalRecall. Each file is one independently-grabbable vertical slice
(tracer bullet) derived from [`../ROADMAP.md`](../ROADMAP.md), with acceptance criteria. All slices
are **AFK** (no human review gate). Work them in dependency order.

| # | Title | Phase | Blocked by | Status |
|--:|-------|:-----:|------------|--------|
| [0001](0001-walking-skeleton.md) | Walking skeleton (+ chat placeholder + baseline telemetry) | 0 | — | done |
| [0002](0002-local-auth-cookie-session.md) | Local auth: register/login → cookie session | 1 | 0001 | done |
| [0003](0003-roles-and-admin-gate.md) | Roles & admin gate | 1 | 0002 | done |
| [0004](0004-create-view-session-privacy.md) | Create & view a Session (Raw autosave) + per-user privacy | 2 | 0002 | done |
| [0005](0005-raw-revision-history.md) | Raw Revision history | 2 | 0004 | done |
| [0006](0006-timeline-querykit-journaling-day.md) | Timeline + QueryKit filters + journaling-day | 2 | 0004 | done |
| [0007](0007-port-journalrecall-ai.md) | Port `JournalRecall.AI` agent framework | 3 | 0001 | done |
| [0008](0008-ai-cleanup-cleaned-synopsis.md) | AI Cleanup → Cleaned + Synopsis | 4 | 0007, 0005 | done |
| [0009](0009-corrections.md) | Corrections | 4 | 0008 | done |
| [0010](0010-edit-cleaned-rerun-warn-history.md) | Edit Cleaned + re-run warn-and-overwrite + history | 4 | 0008 | done |
| [0011](0011-manual-metadata-filtering.md) | Manual metadata (Topics, People, Mood) + filtering | 5 | 0004, 0006 | done |
| [0012](0012-ai-metadata-suggestions.md) | AI metadata Suggestions (accept/reject) | 5 | 0008, 0011 | done |
| [0013](0013-day-week-summaries.md) | Day & Week Summaries (on-demand) | 6 | 0007, 0004 | done |
| [0014](0014-period-rollups-staleness.md) | Month/Quarter/Year roll-ups + staleness propagation | 6 | 0013 | done |
| [0015](0015-location-opt-in.md) | Location opt-in | 7 | 0004 | done |
| [0016](0016-admin-surface.md) | Admin surface: user management + AI provider config | 7 | 0003, 0007 | done |
| [0017](0017-ai-lifecycle-observability.md) | AI-lifecycle observability + redaction | 7 | 0008 | done |
| [0018](0018-single-container-deployment.md) | Single-container deployment | 8 | 0001 | done |
| [0019](0019-refresh-token-rotation-durable-sessions.md) | Refresh-token rotation & durable sessions | 9 | 0002 | ready |
| [0020](0020-cookie-hardening-csrf-client-interceptor.md) | Cookie hardening (`__Host-`/`__Secure-`, `X-CSRF`) + client single-flight refresh | 9 | 0019 | ready |
| [0021](0021-first-run-setup-root-admin.md) | First-run setup & root Admin | 9 | 0019 | ready |
| [0022](0022-access-gate-public-auth-config.md) | Access gate (server + client) & public auth config | 9 | 0021 | ready |
| [0023](0023-operator-controlled-registration.md) | Operator-controlled registration | 9 | 0022 | ready |
| [0024](0024-temp-passwords-forced-change.md) | Temporary passwords & forced password change | 9 | 0019, 0016 | ready |
| [0025](0025-fix-setup-stale-config-redirect.md) | Fix `/setup` stale-config redirect after root-Admin creation | 9 | — | ready |
| [0026](0026-valueobject-base-username-value-object.md) | `ValueObject` base class + `Username` value object | 9 | — | ready |
| [0027](0027-username-replaces-email-identity.md) | Replace email with username as the sole identity | 9 | 0026 | ready |
| [TEST-0001](TEST-0001-test-suite-scaffold-four-projects-adr.md) | Test-suite scaffold: four projects, packages, conventions, ADR-0006 | 10 | — | ready |
| [TEST-0002](TEST-0002-sharedtesthelpers-builders-fakers-unit-proof.md) | SharedTestHelpers builders/fakers + unit-layer proof | 10 | TEST-0001 | ready |
| [TEST-0003](TEST-0003-integration-harness-reference-tests.md) | Integration harness + reference tests | 10 | TEST-0002 | ready |
| [TEST-0004](TEST-0004-functional-harness-reference-tests.md) | Functional harness + reference tests | 10 | TEST-0002 | ready |
| [TEST-0005](TEST-0005-tests-readme-decision-tree.md) | `tests/README.md` decision tree | 10 | TEST-0003, TEST-0004 | ready |
| [TEST-0006](TEST-0006-migrate-domain-tests-to-unit.md) | Migrate domain tests → UnitTests | 10 | TEST-0002 | ready |
| [TEST-0007](TEST-0007-migrate-sessions-area.md) | Migrate Sessions area (sessions, revisions, timeline, journaling-day) | 10 | TEST-0003, TEST-0004 | ready |
| [TEST-0008](TEST-0008-migrate-metadata-location.md) | Migrate Metadata & Location | 10 | TEST-0003, TEST-0004 | ready |
| [TEST-0009](TEST-0009-migrate-cleanup-corrections.md) | Migrate Cleanup & Corrections | 10 | TEST-0003, TEST-0004 | ready |
| [TEST-0010](TEST-0010-migrate-summaries.md) | Migrate Summaries | 10 | TEST-0003, TEST-0004 | ready |
| [TEST-0011](TEST-0011-migrate-core-auth.md) | Migrate core auth (login, refresh, cookie hardening, forced change) | 10 | TEST-0004 | ready |
| [TEST-0012](TEST-0012-migrate-access-setup-admin.md) | Migrate access gate, setup, admin & registration | 10 | TEST-0004 | ready |
| [TEST-0013](TEST-0013-migrate-health-observability.md) | Migrate Health & Observability | 10 | TEST-0003, TEST-0004 | ready |
| [TEST-0014](TEST-0014-retire-api-tests.md) | Retire `Api.Tests` | 10 | TEST-0006–TEST-0013 | ready |
| [FORM-001](FORM-001-deps-forms-pattern-adr.md) | Dependencies + forms-pattern ADR | 11 | — | done |
| [FORM-002](FORM-002-problemerror-problemdetails-parser.md) | `ProblemError` + ProblemDetails parser (API-client seam) | 11 | — | done |
| [FORM-003](FORM-003-apply-server-errors-helper.md) | `applyServerErrors(form, error)` helper | 11 | FORM-001, FORM-002 | done |
| [FORM-004](FORM-004-shared-schema-fragments.md) | Shared schema fragments (password + email) | 11 | FORM-001 | done |
| [FORM-005](FORM-005-bound-field-components-formshell.md) | Bound field components + `FormShell` | 11 | FORM-001 | done |
| [FORM-006](FORM-006-convert-login-form.md) | Convert login form | 11 | FORM-003, FORM-004, FORM-005 | done |
| [FORM-007](FORM-007-convert-register-setup-forms.md) | Convert register + setup forms | 11 | FORM-003, FORM-004, FORM-005 | done |
| [FORM-008](FORM-008-convert-change-password-form.md) | Convert change-password form | 11 | FORM-003, FORM-004, FORM-005 | done |
| [FORM-009](FORM-009-convert-corrections-create-form.md) | Convert Corrections create form | 11 | FORM-003, FORM-005 | done |
| [FORM-010](FORM-010-convert-create-user-form.md) | Convert create-user (admin) form | 11 | FORM-003, FORM-004, FORM-005 | done |
| [FORM-011](FORM-011-convert-ai-provider-config-form.md) | Convert AI-provider config form | 11 | FORM-003, FORM-005 | done |
| [FORM-012](FORM-012-convert-session-metadata-editor.md) | Convert Session Metadata editor | 11 | FORM-003, FORM-005 | done |
| [FE-001](FE-001-queryoptions-auth-factories.md) | Auth `queryOptions()` factories + collapse root `beforeLoad` duplication | 12 | — | ready |
| [FE-002](FE-002-queryoptions-feature-factories.md) | `queryOptions()` factories for the remaining feature queries | 12 | FE-001 | ready |
| [FE-003](FE-003-auth-selectors-isadmin.md) | Auth selectors `useIsAdmin`/`useAuthRoles` + de-duplicate Admin-role rule | 12 | FE-001 | ready |
| [FE-004](FE-004-me-hook-select-param.md) | Optional `select` parameter on the shared `me` hook | 12 | FE-003 | ready |
| [FE-005](FE-005-auth-config-session-selectors.md) | *(NICE)* auth-config slice + single-item session selectors | 12 | FE-002, FE-004 | ready |
| [FE-006](FE-006-admin-gate-beforeload.md) | Move the Admin role gate into the Admin route `beforeLoad` | 12 | FE-001, FE-003 | ready |
| [FE-007](FE-007-loaders-admin-corrections.md) | `ensureQueryData` loaders on the Admin + Corrections routes | 12 | FE-002 | ready |
| [FE-008](FE-008-session-detail-loader.md) | Session-detail loader + non-awaited Revision-stream prefetch | 12 | FE-002 | ready |
| [FE-009](FE-009-timeline-filters-url-search.md) | Timeline Topic/Person/Mood filters as URL search state | 12 | FE-002 | ready |
| [FE-010](FE-010-summary-period-url-search.md) | Summary period/date as URL search state | 12 | FE-002 | ready |
| [FE-011](FE-011-router-pending-error-components.md) | Router-level default pending/error components | 12 | FE-007, FE-008 | ready |
| [FE-012](FE-012-default-preload-stale-time.md) | *(NICE)* `defaultPreloadStaleTime: 0` | 12 | FE-007, FE-008 | ready |
| [FE-013](FE-013-session-editor-key-remount.md) | Reset the Session editor by `key` on Session identity (stale-text fix) | 12 | — | ready |
| [FE-014](FE-014-key-metadata-aiprovider-forms.md) | Key the Metadata editor + Admin AI-provider form on entity identity | 12 | — | ready |
| [FE-015](FE-015-cleaned-editor-change-token.md) | Simplify the Cleaned editor reconciler to `key` + change-token | 12 | FE-013 | ready |
| [FE-016](FE-016-timezone-default-derive.md) | Derive the timezone default at render, not via a write effect | 12 | — | ready |
| [FE-017](FE-017-resolve-session-settings-edge.md) | Resolve the Session→settings cross-feature import | 12 | — | ready |
| [FE-018](FE-018-feature-public-api-barrels.md) | Per-feature public-API barrels | 12 | — | ready |
| [FE-019](FE-019-eslint-import-boundaries.md) | ESLint flat config + import-boundary rule (error) + `lint` script & CI | 12 | FE-017, FE-018 | ready |
| [FE-020](FE-020-extract-session-detail-route.md) | Extract the Session-detail route into a feature component | 12 | FE-013 | ready |
| [FE-021](FE-021-extract-admin-route.md) | Extract the Admin route into feature component(s) | 12 | — | ready |
| [FE-022](FE-022-extract-summaries-corrections-routes.md) | Extract the Summaries + Corrections routes into feature components | 12 | — | ready |
| [FE-023](FE-023-shared-criteria-naming-lint-guardrail.md) | *(NICE)* promote-to-shared criterion, naming, derive-don't-sync guardrail | 12 | FE-018, FE-019 | ready |
| [FE-024](FE-024-adr-0008-compound-forms.md) | ADR-0008: compound form components (supersedes ADR-0007) | 12 | — | ready |
| [FE-025](FE-025-form-compound-context.md) | Form compound context + throwing `useFormContext()` + `Form.Submit`/`Form.Errors` | 12 | FE-024 | ready |
| [FE-026](FE-026-createform-factory-typed.md) | `createForm<Schema>()` factory + typed `applyServerErrors` | 12 | FE-025 | ready |
| [FE-027](FE-027-migrate-forms-compound-api.md) | Migrate existing forms to the compound API | 12 | FE-026 | ready |
| [FE-028](FE-028-dev-browser-helper-module.md) | Committed dev-browser helper module (`login`, `completeSetup`, base-URL) | 12 | — | ready |
| [FE-029](FE-029-dev-runbook-rewrite.md) | Rewrite the dev-runbook e2e guidance | 12 | FE-028 | ready |
| [FE-030](FE-030-e2e-polish-shim-catalog.md) | *(NICE)* `waitFor` shim, reference-flow catalog, failure capture | 12 | FE-028 | ready |

## Suggested order

Tracer bullet first: **0001 → 0002 → 0004** gives a usable, private journal (write + re-read) with
no AI. **0007** (library port) can run in parallel after 0001. AI features (0008–0012), Summaries
(0013–0014), and the geo/admin/observability/deploy slices follow.

**Phase 9** (auth onboarding & durable sessions, realizing PRD-0001 / ADR-0005): **0019 → 0020 →
0021 → 0022 → 0023**, with **0024** able to run in parallel after **0019**. Follow-ups: **0025** (setup
redirect fix) is independent and can start immediately; the username-identity switch is **0026**
(`ValueObject` + `Username`) → **0027** (replace email with username end-to-end).

**Phase 10** (three-layer test suite + builders/fakers, realizing PRD-0003 / ADR-0006): scaffold
first — **TEST-0001 → TEST-0002**, then the two harnesses **TEST-0003** and **TEST-0004** in parallel, with **TEST-0005**
(docs) after both. Phase 2 migration runs once **TEST-0003/TEST-0004** land: **TEST-0006** (domain → unit) needs
only **TEST-0002**; the per-area migrations (**TEST-0007–TEST-0010**, **TEST-0013**) need both harnesses, while the
auth-area migrations (**TEST-0011**, **TEST-0012**) need only the functional harness. **TEST-0014** (delete
`Api.Tests`) is the capstone — it blocks on every migration (**TEST-0006–TEST-0013**).

**Phase 11** (forms on `@tanstack/react-form` + zod, realizing PRD-0004): big-bang — build the shared
modules first, then convert all eight forms. Foundation: **FORM-001** (deps + ADR), **FORM-002**
(`ProblemError`) can start immediately and in parallel; **FORM-003** (`applyServerErrors`) needs
FORM-001 + FORM-002; **FORM-004** (schema fragments) and **FORM-005** (field components + `FormShell`)
need only FORM-001. Once FORM-003/004/005 land, the seven conversions (**FORM-006–FORM-012**) all run
in parallel — auth forms (**FORM-006/007/008**) and create-user (**FORM-010**) also need the schema
fragments (FORM-004).

**Phase 12** (frontend pattern hardening, realizing PRD-0005): keystone first — **FE-001**
(`queryOptions()` auth factories + root `beforeLoad` de-dup) then **FE-002** (the rest of the
factories) — which unblock the loaders and selectors. Six mostly-independent work-streams follow:
*selectors* (**FE-003** → **FE-004** → **FE-005**), *router+query* (**FE-006** admin gate needs
FE-001/003; **FE-007/008** loaders need FE-002, then **FE-011** default pending/error and **FE-012**
preload), *URL search state* (**FE-009/010** off FE-002), *derive-from-server* (**FE-013** Session
editor `key`-remount is the highest-priority single fix and gates **FE-015** and the route extraction
**FE-020**; **FE-014/016** are independent), *boundaries* (**FE-017** settings-edge + **FE-018**
barrels gate **FE-019** ESLint-as-error — note the rule flags cross-layer *imports*, not file size, so
the route extractions **FE-020/021/022** are independent quality work; **FE-023** is NICE docs/guardrails),
*compound forms* (**FE-024** ADR-0008 → **FE-025** context → **FE-026** `createForm` factory →
**FE-027** migrate the forms), and *e2e* (**FE-028** helper module → **FE-029** runbook rewrite,
**FE-030** NICE polish). NICE slices (**FE-005/012/023/030**) are optional.
