using System.Diagnostics;

namespace JournalRecall.Api.Observability;

/// <summary>
/// Holds a root <see cref="Activity"/> together with the ambient activity it displaced, and puts that
/// ambient activity back on dispose. <see cref="ApiTelemetry.TraceRoot"/> returns this scope, so a
/// <c>using</c> block both ends the root span and restores the caller's trace in one step.
/// </summary>
public sealed class ActivityScope(Activity? activity, Activity? previousActivity) : IDisposable
{
    /// <summary>The started root span, or null when no listener sampled it.</summary>
    public Activity? Activity { get; } = activity;

    public void Dispose()
    {
        Activity?.Dispose();
        System.Diagnostics.Activity.Current = previousActivity;
    }
}
