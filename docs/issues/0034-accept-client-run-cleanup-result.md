# 0034 — Accept a client-run Cleanup result (OnDevice Engine)

**Phase:** 9 (mobile sync) · **Type:** AFK · **Status:** ready · **Realizes:** ADR-0013, CONTEXT.md "Engine" · **Paired with:** journal-recall-ios#0008

## What to build

The server half of the **OnDevice Engine**: `POST /sessions/{id}/cleanup/result` accepts the same
shape the CleanupAgent emits (`cleanedMarkdown`, `synopsis`, topic/mood suggestions, people
proposal), plus the `baseRawRevisionNumber` the device cleaned against and the engine identifier.
The server then post-processes **identically to a server-run Cleanup** — hard-replace Corrections,
markdown → ProseMirror conversion, People resolution/proposal parking per the user's approval
setting, Cleaned Revision append, Synopsis and suggestion storage, `CleanupStatus` set, affected
period Summaries marked Stale. In the domain the outcome is indistinguishable from a server run;
Stale derivation works naturally off the submitted base revision (Raw edited since → still Stale).

## Acceptance criteria

- [ ] Submitting a result produces the same persisted state as a server-run Cleanup over the same
      Raw text and Corrections (parity covered by tests).
- [ ] Hard-replace Corrections are applied server-side even if the device missed them.
- [ ] A result based on an older Raw Revision is recorded, and the Session correctly derives
      `Stale`.
- [ ] People proposals respect `requirePeopleTagApproval` exactly as in a server run.
- [ ] The affected period Summaries go Stale.
- [ ] Invalid payloads fail with a validation error and leave the Session untouched.

## Blocked by

None — can start immediately.
