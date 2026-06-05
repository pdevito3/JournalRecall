using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Domain.Sessions.Ai;

namespace JournalRecall.Api.Tests;

/// <summary>
/// A deterministic stand-in for the Cleanup model: by default it echoes the Raw text back as a
/// "Polished: …" Cleaned copy plus a Synopsis, shaped as the JSON the agent expects. Flip
/// <see cref="Throw"/> to simulate a model failure. Sequential within a test class, so the mutable
/// switches are safe.
/// </summary>
internal sealed class ScriptableChatClient : IChatClient
{
    public bool Throw { get; set; }
    public string? CleanedOverride { get; set; }
    public string Synopsis { get; set; } = "A short recap of the session.";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Throw)
            throw new InvalidOperationException("simulated model failure");

        var raw = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
        var cleaned = CleanedOverride ?? $"Polished: {raw}";
        var json = JsonSerializer.Serialize(new { cleaned, synopsis = Synopsis });

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
        {
            Usage = new UsageDetails { TotalTokenCount = 5 },
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
            yield return new ChatResponseUpdate(message.Role, message.Contents);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>Boots the real app but swaps the keyed "cleanup" <see cref="IChatClient"/> for a scripted fake.</summary>
public sealed class CleanupWebApplicationFactory : SkeletonWebApplicationFactory
{
    internal ScriptableChatClient Chat { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services) =>
        // Registered after the app's lazy AddChatModel("cleanup", …) → this keyed instance wins.
        services.AddKeyedSingleton<IChatClient>(CleanupAgent.ModelKey, Chat);
}
