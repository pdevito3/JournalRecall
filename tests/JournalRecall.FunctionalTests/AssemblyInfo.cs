// Functional tests boot the full host (process-global Serilog/OpenTelemetry state) and share hosts via a
// single collection fixture. Run serially so concurrent startups don't collide (ADR-0006).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
