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
///
/// Config rotation is safe for in-flight calls (issue 0031): the request/stream runs outside the lock, so
/// disposing the inner client at the moment of a swap could pull it out from under a concurrent Cleanup or
/// Summary call mid-await (<see cref="ObjectDisposedException"/>). Instead each resolved client is held in a
/// ref-counted <see cref="Lease"/>; a swap (or <see cref="Dispose"/>) only disposes a client once its last
/// in-flight caller has released it. A request that began before the swap completes against the client it
/// captured; a request after the swap uses the new one.
/// </summary>
public sealed class ConfigurableChatClient : IChatClient
{
    private readonly object _gate = new();
    private readonly Func<CancellationToken, Task<ChatModelOptions>> _resolveOptions;
    private readonly Func<ChatModelOptions, IChatClient> _createClient;
    private string? _signature;
    private Lease? _current;
    private bool _disposed;

    public ConfigurableChatClient(IServiceScopeFactory scopeFactory, ChatModelOptions fallback)
        : this(
            async ct =>
            {
                // A fresh scope so the scoped DbContext lifetime is independent of the agent run's scope.
                using var scope = scopeFactory.CreateScope();
                var effective = scope.ServiceProvider.GetRequiredService<EffectiveChatModelOptions>();
                return await effective.ResolveAsync(fallback, ct);
            },
            OpenAIChatModelExtensions.CreateChatClient)
    {
    }

    /// <summary>Test seam: drive resolution and client construction directly (no DI scope or real provider).</summary>
    internal ConfigurableChatClient(
        Func<CancellationToken, Task<ChatModelOptions>> resolveOptions,
        Func<ChatModelOptions, IChatClient> createClient)
    {
        _resolveOptions = resolveOptions;
        _createClient = createClient;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lease = await AcquireAsync(cancellationToken);
        try
        {
            return await lease.Client.GetResponseAsync(messages, options, cancellationToken);
        }
        finally
        {
            Release(lease);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lease = await AcquireAsync(cancellationToken);
        try
        {
            await foreach (var update in lease.Client.GetStreamingResponseAsync(messages, options, cancellationToken))
                yield return update;
        }
        finally
        {
            Release(lease);
        }
    }

    /// <summary>
    /// Resolves the effective options and returns the matching inner client with its in-flight count
    /// incremented, so a concurrent swap won't dispose it before this caller is done. The caller must
    /// <see cref="Release"/> the returned lease.
    /// </summary>
    private async Task<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        var options = await _resolveOptions(cancellationToken);
        var signature = $"{options.Provider}|{options.Endpoint}|{options.Model}|{options.ApiKey}";

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_current is null || signature != _signature)
            {
                // Swap: hand the old client to whoever still holds it; dispose only once nobody does.
                _current?.MarkForDisposalAndReleaseOwner();
                _current = new Lease(_createClient(options));
                _signature = signature;
            }

            _current.AddRef();
            return _current;
        }
    }

    private void Release(Lease lease)
    {
        lock (_gate)
            lease.Release();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            // Drop our ownership reference; the client is disposed here only if no request still holds it.
            _current?.MarkForDisposalAndReleaseOwner();
            _current = null;
        }
    }

    /// <summary>
    /// A single resolved inner client plus the count of references holding it alive. The owning
    /// <see cref="ConfigurableChatClient"/> holds one reference (released on swap or dispose); each in-flight
    /// request holds one more. The client is disposed by whichever reference is the last to drop once it has
    /// been marked for disposal. All members are called under <see cref="_gate"/>.
    /// </summary>
    private sealed class Lease(IChatClient client)
    {
        public IChatClient Client { get; } = client;

        // Starts at 1 for the owner's reference.
        private int _refCount = 1;
        private bool _markedForDisposal;

        public void AddRef() => _refCount++;

        public void Release()
        {
            if (--_refCount == 0 && _markedForDisposal)
                Client.Dispose();
        }

        /// <summary>Marks the client for disposal and drops the owner's reference (disposing if it was the last).</summary>
        public void MarkForDisposalAndReleaseOwner()
        {
            _markedForDisposal = true;
            Release();
        }
    }
}
