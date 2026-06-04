# Issues

In-repo issue tracker for JournalRecall. Each file is one independently-grabbable vertical slice
(tracer bullet) derived from [`../ROADMAP.md`](../ROADMAP.md), with acceptance criteria. All slices
are **AFK** (no human review gate). Work them in dependency order.

| # | Title | Phase | Blocked by | Status |
|--:|-------|:-----:|------------|--------|
| [0001](0001-walking-skeleton.md) | Walking skeleton (+ chat placeholder + baseline telemetry) | 0 | — | done |
| [0002](0002-local-auth-cookie-session.md) | Local auth: register/login → cookie session | 1 | 0001 | todo |
| [0003](0003-roles-and-admin-gate.md) | Roles & admin gate | 1 | 0002 | todo |
| [0004](0004-create-view-session-privacy.md) | Create & view a Session (Raw autosave) + per-user privacy | 2 | 0002 | todo |
| [0005](0005-raw-revision-history.md) | Raw Revision history | 2 | 0004 | todo |
| [0006](0006-timeline-querykit-journaling-day.md) | Timeline + QueryKit filters + journaling-day | 2 | 0004 | todo |
| [0007](0007-port-journalrecall-ai.md) | Port `JournalRecall.AI` agent framework | 3 | 0001 | todo |
| [0008](0008-ai-cleanup-cleaned-synopsis.md) | AI Cleanup → Cleaned + Synopsis | 4 | 0007, 0005 | todo |
| [0009](0009-corrections.md) | Corrections | 4 | 0008 | todo |
| [0010](0010-edit-cleaned-rerun-warn-history.md) | Edit Cleaned + re-run warn-and-overwrite + history | 4 | 0008 | todo |
| [0011](0011-manual-metadata-filtering.md) | Manual metadata (Topics, People, Mood) + filtering | 5 | 0004, 0006 | todo |
| [0012](0012-ai-metadata-suggestions.md) | AI metadata Suggestions (accept/reject) | 5 | 0008, 0011 | todo |
| [0013](0013-day-week-summaries.md) | Day & Week Summaries (on-demand) | 6 | 0007, 0004 | todo |
| [0014](0014-period-rollups-staleness.md) | Month/Quarter/Year roll-ups + staleness propagation | 6 | 0013 | todo |
| [0015](0015-location-opt-in.md) | Location opt-in | 7 | 0004 | todo |
| [0016](0016-admin-surface.md) | Admin surface: user management + AI provider config | 7 | 0003, 0007 | todo |
| [0017](0017-ai-lifecycle-observability.md) | AI-lifecycle observability + redaction | 7 | 0008 | todo |
| [0018](0018-single-container-deployment.md) | Single-container deployment | 8 | 0001 | todo |

## Suggested order

Tracer bullet first: **0001 → 0002 → 0004** gives a usable, private journal (write + re-read) with
no AI. **0007** (library port) can run in parallel after 0001. AI features (0008–0012), Summaries
(0013–0014), and the geo/admin/observability/deploy slices follow.
