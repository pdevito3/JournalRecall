# 0017 — AI-lifecycle observability + redaction

**Phase:** 7 · **Type:** AFK · **Status:** todo · **Realizes:** ADR-0002

## What to build

Deepen observability over the AI lifecycle (building on the baseline telemetry from #0001): spans
and structured logs across Cleanup/Summary runs, with content capture opt-in and a redaction hook —
safe by default for an intimate-journal domain.

- `ActivitySource` spans for the AI outer loop / cleanup / summary generation; structured logging
  subscribers on the agent event stream.
- Telemetry policy: metadata always (model, token usage, tool names, latency, finish reason, error
  type); prompt/response **content capture opt-in per environment**, passing through a pluggable
  redaction hook before export.

## Acceptance criteria

- [ ] A Cleanup run emits spans covering the run with model/token/latency metadata attached.
- [ ] Content capture is **off** by default; when enabled, content passes through the redaction hook
      before export (verified with a test redactor).
- [ ] Structured logs correlate to the run (correlation id present).
- [ ] No journal content is exported when capture is off (assertion test).

## Blocked by

- #0008
