using Microsoft.Extensions.AI;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Domain.Admin.Services;

namespace JournalRecall.Api.Domain.Admin.Services;

/// <summary>
/// The keyed <see cref="IChatClient"/> the Cleanup/Summary agents resolve, backed by the Admin-configured
/// app-wide provider rather than a startup-fixed one (issue 0016). Each call resolves the effective
/// options (stored config, else the appsettings fallback) and delegates to a real client, rebuilding the
/// inner client only when those options change. So a "subsequent Cleanup uses the configured provider"
/// after an Admin updates it — no restart needed. Registered before the test override so a scripted fake
/// still wins in tests.
/// </summary>
public sealed class ConfigurableChatClient(
    IServiceScopeFactory scopeFactory, ChatModelOptions fallback) : IChatClient
{
    private readonly object _gate = new();
    private string? _signature;
    private IChatClient? _inner;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveAsync(cancellationToken);
        return await client.GetResponseAsync(messages, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await ResolveAsync(cancellationToken);
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
            yield return update;
    }

    private async Task<IChatClient> ResolveAsync(CancellationToken cancellationToken)
    {
        // A fresh scope so the scoped DbContext lifetime is independent of the agent run's scope.
        using var scope = scopeFactory.CreateScope();
        var effective = scope.ServiceProvider.GetRequiredService<EffectiveChatModelOptions>();
        var options = await effective.ResolveAsync(fallback, cancellationToken);

        var signature = $"{options.Provider}|{options.Endpoint}|{options.Model}|{options.ApiKey}";
        lock (_gate)
        {
            if (_inner is null || signature != _signature)
            {
                _inner?.Dispose();
                _inner = OpenAIChatModelExtensions.CreateChatClient(options);
                _signature = signature;
            }
            return _inner;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        lock (_gate)
            _inner?.Dispose();
    }
}
