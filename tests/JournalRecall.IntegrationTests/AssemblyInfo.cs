// Integration tests share one SQLite file and one booted host with mutable scriptable fakes. They run in
// a single serial collection; disabling assembly parallelization keeps that guarantee explicit (ADR-0006).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
