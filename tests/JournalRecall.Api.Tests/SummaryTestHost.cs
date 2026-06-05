using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Summaries.Ai;

namespace JournalRecall.Api.Tests;

/// <summary>
/// A deterministic stand-in for the Summary model: it records the prompt it last received (so tests can
/// assert which Session text — Cleaned vs Raw — fed the run) and returns a fixed narrative as plain text.
/// </summary>
internal sealed class ScriptableSummaryChatClient : IChatClient
{
    public string Narrative { get; set; } = "A reflective recap of the period.";

    /// <summary>The user prompt text the model last received — lets tests assert the source used.</summary>
    public string LastUserText { get; private set; } = string.Empty;

    /// <summary>How many times the model has been invoked — lets tests assert generate vs refresh.</summary>
    public int CallCount { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var all = messages.ToList();
        LastUserText = string.Join("\n", all.Where(m => m.Role == ChatRole.User).Select(m => m.Text));
        CallCount++;

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Narrative))
        {
            Usage = new UsageDetails { TotalTokenCount = 7 },
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

/// <summary>Boots the real app with scripted fakes for both the "cleanup" and "summary" chat models.</summary>
public sealed class SummaryWebApplicationFactory : SkeletonWebApplicationFactory
{
    internal ScriptableChatClient Cleanup { get; } = new();
    internal ScriptableSummaryChatClient Summary { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddKeyedSingleton<IChatClient>(CleanupAgent.ModelKey, Cleanup);
        services.AddKeyedSingleton<IChatClient>(SummaryAgent.ModelKey, Summary);
    }
}
