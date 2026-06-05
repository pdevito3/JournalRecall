# 0037 — Migrate Health & Observability

**Phase:** 10 · **Type:** AFK · **Status:** ready · **Realizes:** PRD-0003

## What to build

Migrate the health-endpoint, health-telemetry, and AI-observability tests into the right layer. The
health endpoints need the web host (functional); AI-lifecycle observability/redaction assertions go
integration or functional depending on whether HTTP is the thing under test. Suite stays green.

- Migrate `HealthEndpointTests`, `HealthTelemetryTests`, `AiObservabilityTests` from `Api.Tests` into
  `FunctionalTests/{Area}/` and/or `IntegrationTests/FeatureTests/`.
- AI-lifecycle observability/redaction driven via the shared `ScriptableChatClient` where no HTTP is
  needed; health endpoints + telemetry stay functional.
- Lowercase names; Shouldly assertions.

## Acceptance criteria

- [ ] Health-endpoint, health-telemetry, and AI-observability tests live in the functional and/or
      integration layer per the decision tree, named `lowercase_with_underscores`, using Shouldly.
- [ ] Observability/redaction behavior remains covered; AI flows use the shared `ScriptableChatClient`.
- [ ] The migrated files are removed from `Api.Tests`; the full suite is green.

## Blocked by

- #0027
- #0028
