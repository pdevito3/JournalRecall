using Shouldly;
using JournalRecall.Api.Domain.Summaries;

namespace JournalRecall.Api.Tests.Domain;

/// <summary>Pure unit tests for the <see cref="Summary"/> aggregate's generation + staleness state.</summary>
public class SummaryAggregateTests
{
    private static Summary New() =>
        Summary.Create(Guid.CreateVersion7(), SummaryPeriod.Day, new DateOnly(2026, 6, 10));

    [Fact]
    public void A_new_summary_is_generating_with_its_period_and_anchor()
    {
        var s = New();
        s.Period.ShouldBe(SummaryPeriod.Day);
        s.PeriodDate.ShouldBe(new DateOnly(2026, 6, 10));
        s.Status.ShouldBe(SummaryStatus.Generating);
        s.GeneratedAt.ShouldBeNull();
    }

    [Fact]
    public void Completing_a_run_makes_it_ready_with_content_count_and_a_timestamp()
    {
        var s = New();
        s.Complete("a recap", sourceSessionCount: 3);

        s.Status.ShouldBe(SummaryStatus.Ready);
        s.Content.ShouldBe("a recap");
        s.SourceSessionCount.ShouldBe(3);
        s.GeneratedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Mark_stale_only_affects_a_ready_summary()
    {
        var s = New();

        s.MarkStale(); // Generating → unchanged
        s.Status.ShouldBe(SummaryStatus.Generating);

        s.Complete("recap", 1);
        s.MarkStale(); // Ready → Stale
        s.Status.ShouldBe(SummaryStatus.Stale);

        s.MarkStale(); // already Stale → no-op
        s.Status.ShouldBe(SummaryStatus.Stale);
    }

    [Fact]
    public void Re_generating_a_stale_summary_returns_it_to_ready()
    {
        var s = New();
        s.Complete("v1", 1);
        s.MarkStale();

        s.BeginGeneration();
        s.Status.ShouldBe(SummaryStatus.Generating);
        s.Complete("v2", 2);
        s.Status.ShouldBe(SummaryStatus.Ready);
        s.Content.ShouldBe("v2");
    }
}
