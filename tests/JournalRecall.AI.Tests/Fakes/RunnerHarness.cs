using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using Polly;
using Polly.Registry;
using Polly.Retry;

namespace JournalRecall.AI.Tests.Fakes;

/// <summary>Builds a real <see cref="IAgentRunner"/> wired to test fakes (model, capabilities, time, resilience).</summary>
internal static class RunnerHarness
{
    public const string ModelName = "fast";

    public static IAgentRunner Build(
        FakeChatClient client,
        ICapabilityResolver? capabilities = null,
        TimeProvider? timeProvider = null,
        bool retryAnyFaultInstantly = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Register overrides BEFORE AddJournalRecallAgents so its TryAdd calls become no-ops.
        if (capabilities is not null)
            services.AddSingleton(capabilities);
        if (timeProvider is not null)
            services.AddSingleton(timeProvider);
        if (retryAnyFaultInstantly)
            services.AddSingleton(InstantRetryRegistry());

        services.AddKeyedSingleton<IChatClient>(ModelName, client);
        services.AddJournalRecallAgents();

        return services.BuildServiceProvider().GetRequiredService<IAgentRunner>();
    }

    private static ResiliencePipelineRegistry<string> InstantRetryRegistry()
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder(ResilienceKeys.Model, (builder, _) =>
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
            }));
        return registry;
    }
}
