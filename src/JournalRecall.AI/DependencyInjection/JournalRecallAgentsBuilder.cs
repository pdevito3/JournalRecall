using Microsoft.Extensions.DependencyInjection;

namespace JournalRecall.AI.DependencyInjection;

internal sealed class JournalRecallAgentsBuilder(IServiceCollection services) : IJournalRecallAgentsBuilder
{
    public IServiceCollection Services { get; } = services;
}
