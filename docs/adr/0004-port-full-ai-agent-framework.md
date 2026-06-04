# Port the full PlateWise.AI agent framework in v1

## Status

accepted

## Context & decision

`PlateWise.AI` is a full multi-turn, tool-calling **agent runner** (capabilities, per-tool
authorization, an event-streamed outer loop). JournalRecall's v1 AI needs — Cleanup, Synopsis,
Summaries, metadata Suggestions — are **single-shot, structured-output transformations** that don't
need any of that machinery. We nonetheless **port the whole framework now** (renamed to
`JournalRecall.AI`) rather than ship a thin `Microsoft.Extensions.AI` wrapper, expressing v1's
single-shot features through the agent/tool runner (tools as thin adapters over MediatR features,
as the template wires them).

## Considered options

- **Thin M.E.AI wrapper now, agent runner later** — minimal surface for v1, add the runner when the
  chat/RAG phase needs it. Rejected: would mean a second migration and divergence from a proven
  library.

## Consequences

- We **carry agent/tool/authorization surface that v1 does not exercise** — accepted in exchange for
  reusing a battle-tested library and avoiding a later rewrite.
- The deferred **chat/RAG page** (multi-turn + retrieval tools) drops straight into this runner when
  built — the main payoff for porting early.
- v1 AI stays **server-side, BYO OpenAI-compatible endpoint, Admin-configured**; the AI boundary is
  kept clean enough that a future mobile client could run cleanup/summary **on-device** instead of
  calling the server.
