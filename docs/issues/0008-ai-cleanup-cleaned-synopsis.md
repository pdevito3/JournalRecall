# 0008 — AI Cleanup → Cleaned + Synopsis

**Phase:** 4 · **Type:** AFK · **Status:** todo · **Realizes:** ADR-0003, ADR-0004

## What to build

A manual **Cleanup** run that reads a Session's Raw text and produces a **Cleaned** copy and a
**Synopsis**, without ever altering Raw. Surfaces cleanup status and the **Stale** indicator, and
streams progress to the UI.

- Cleanup expressed as an agent/tool over `IChatClient` (per ADR-0004) that emits Cleaned content
  (its own Revision stream) + the Session **Synopsis**.
- `Cleanup status` (`NotRun | Running | Clean | Stale | Failed`); **Stale** = latest Raw Revision is
  newer than the last successful Cleanup.
- React: manual **"Clean up with AI"** button; Raw/Cleaned side-by-side view; status + Stale badge;
  **streamed progress** (SignalR/SSE off the agent event stream).

## Acceptance criteria

- [ ] Running Cleanup produces a Cleaned copy and a Synopsis; the Raw text is byte-for-byte
      unchanged afterward.
- [ ] A Cleaned Revision is appended; status becomes `Clean`.
- [ ] Editing Raw after a successful Cleanup flips the status to `Stale` in the UI.
- [ ] Progress is streamed to the client during the run (not a static spinner), ending in a terminal
      state; a model failure yields `Failed` without corrupting Raw or prior Cleaned.
- [ ] Tests assert Raw immutability across Cleanup and correct status transitions.

## Blocked by

- #0007
- #0005
