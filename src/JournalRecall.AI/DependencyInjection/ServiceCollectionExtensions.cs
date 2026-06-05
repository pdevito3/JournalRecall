using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.Registry;
using Polly.Retry;
using JournalRecall.AI.Runtime;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>
/// Entry point for wiring JournalRecall agents into a host's DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the agent runner and supporting services, returning a builder for opt-in
    /// configuration (models, persistence, streaming, telemetry — layered on in later phases).
    /// </summary>
    public static IJournalRecallAgentsBuilder AddJournalRecallAgents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<Observability.TelemetryOptions>();
        services.AddOptions<Transport.StreamingOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IRunContextAccessor, RunContextAccessor>();
        services.TryAddSingleton<ICapabilityResolver, DiCapabilityResolver>();
        services.TryAddSingleton<IAgentRunner, AgentRunner>();

        // In-memory conversation store is the auto-registered default; a satellite or custom store overrides it.
        services.TryAddSingleton<Core.Persistence.IConversationStore, InMemoryConversationStore>();
        services.TryAddSingleton<ConversationThreadRunner>();
        services.TryAddSingleton(BuildResilienceRegistry());
        services.TryAddSingleton<ResiliencePipelineProvider<string>>(
            sp => sp.GetRequiredService<ResiliencePipelineRegistry<string>>());

        return new JournalRecallAgentsBuilder(services);
    }

    private static ResiliencePipelineRegistry<string> BuildResilienceRegistry()
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder(ResilienceKeys.Model, (builder, _) =>
            builder.AddRetry(new RetryStrategyOptions
            {
                // Transient model/HTTP faults only; user cancellation must not be retried (ADR-0006).
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TaskCanceledException>(),
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
            }));
        return registry;
    }
}
