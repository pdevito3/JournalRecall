using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using Polly.Registry;

namespace JournalRecall.AI.Tests.DependencyInjection;

/// <summary>DI-wiring tests: the entry point registers the runner and its supporting services.</summary>
public class AddJournalRecallAgentsTests
{
    [Fact]
    public void AddJournalRecallAgents_registers_resolvable_runner()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddJournalRecallAgents();

        using var provider = services.BuildServiceProvider();
        provider.GetService<IAgentRunner>().ShouldNotBeNull();
    }

    [Fact]
    public void AddJournalRecallAgents_registers_default_supporting_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJournalRecallAgents();

        using var provider = services.BuildServiceProvider();

        provider.GetService<ICapabilityResolver>().ShouldNotBeNull();
        provider.GetService<TimeProvider>().ShouldNotBeNull();
        var pipelines = provider.GetService<ResiliencePipelineProvider<string>>().ShouldNotBeNull();
        pipelines.GetPipeline(ResilienceKeys.Model).ShouldNotBeNull();
    }
}
