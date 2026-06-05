namespace JournalRecall.AI.Runtime;

/// <summary>Well-known keys for the resilience pipelines the runner uses.</summary>
public static class ResilienceKeys
{
    /// <summary>Pipeline guarding transient model/HTTP faults around each model call (ADR-0006).</summary>
    public const string Model = "journalrecall.model";
}
