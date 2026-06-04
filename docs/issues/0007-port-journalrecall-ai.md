# 0007 — Port `JournalRecall.AI` agent framework

**Phase:** 3 · **Type:** AFK (large) · **Status:** todo · **Realizes:** ADR-0004

## What to build

Port the `PlateWise.AI` agent framework wholesale into `JournalRecall.AI` (renamed), so the full
multi-turn, tool-calling runner exists and is green — ready for the AI features and the future
chat/RAG page. v1 features only use single-shot paths, but per ADR-0004 we port the whole thing now.

- `Core/` (pure): `AgentDefinition` + fluent builder, `AgentState`, `RunContext`, `AgentEvent`,
  `AgentOutcome`, `Decide` / `Authorize` / `OnToolError`.
- `Runtime/` (shell): `IAgentRunner` outer loop composing the `Microsoft.Extensions.AI` pipeline
  (keyed `IChatClient` → function invocation → OpenTelemetry), Polly resilience, event stream.
- Capabilities (tools/resources/prompts/delegation) + MCP interop; conversation store port +
  `JournalRecall.AI.EntityFrameworkCore` satellite; streaming transports.
- `AddJournalRecallAgents(...)` DI entry; **BYO OpenAI-compatible** `IChatClient` registration.

## Acceptance criteria

- [ ] The ported test suite is green: pure-core (zero-mock + property) tests, `FakeChatClient`
      runner tests, tool `AIFunction` schema contract tests, and architecture boundary tests.
- [ ] `AddJournalRecallAgents(...)` resolves a runner from DI.
- [ ] A smoke agent runs end-to-end against a `FakeChatClient` and returns an `AgentOutcome`.
- [ ] The library points at a configurable OpenAI-compatible endpoint (verified against a local/
      fake endpoint), with no provider hard-coded.
- [ ] Telemetry metadata (model, tokens, tool names, latency) is emitted; content capture is off by
      default.

## Blocked by

- #0001
