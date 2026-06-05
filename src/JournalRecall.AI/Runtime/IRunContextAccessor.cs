using JournalRecall.AI.Core;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// Scoped accessor exposing the current run's <see cref="RunContext"/> to tools and backing services
/// (e.g. an <c>ICurrentUserService</c> that derives from it — ADR-0009). Mirrors the role of
/// <c>IHttpContextAccessor</c>, without HTTP coupling. The runner sets it when a run's scope opens.
/// </summary>
public interface IRunContextAccessor
{
    RunContext? Current { get; set; }
}

internal sealed class RunContextAccessor : IRunContextAccessor
{
    public RunContext? Current { get; set; }
}
