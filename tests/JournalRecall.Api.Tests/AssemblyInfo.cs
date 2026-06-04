// Integration tests boot the full host, which touches process-global state (Serilog's static
// Log.Logger, the OpenTelemetry provider). Run them serially so concurrent startups don't collide.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
