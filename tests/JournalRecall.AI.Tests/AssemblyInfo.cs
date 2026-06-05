// Run tests sequentially. Several tests use process-global resources (the OpenTelemetry
// ActivitySource/Meter and in-memory MCP client read loops); parallel execution saturates the
// thread pool and starves async notification delivery, making them flaky. Determinism > speed here.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
