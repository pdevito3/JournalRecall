using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>
/// Registers DI-backed capabilities so the resolver can materialize them per run. Capabilities are
/// registered scoped: a run owns a DI scope, letting tools/resources depend on scoped services
/// (e.g. a DbContext or MediatR <c>ISender</c>) — ADR-0003/0009.
/// </summary>
public static class CapabilityRegistrationExtensions
{
    /// <summary>Registers an <see cref="ITool"/> implementation.</summary>
    public static IJournalRecallAgentsBuilder AddTool<T>(this IJournalRecallAgentsBuilder builder) where T : class, ITool
    {
        builder.Services.AddScoped<T>();
        return builder;
    }

    /// <summary>Registers an <see cref="IResource"/> implementation.</summary>
    public static IJournalRecallAgentsBuilder AddResource<T>(this IJournalRecallAgentsBuilder builder) where T : class, IResource
    {
        builder.Services.AddScoped<T>();
        return builder;
    }

    /// <summary>Registers an <see cref="IPrompt"/> implementation.</summary>
    public static IJournalRecallAgentsBuilder AddPrompt<T>(this IJournalRecallAgentsBuilder builder) where T : class, IPrompt
    {
        builder.Services.AddScoped<T>();
        return builder;
    }
}
