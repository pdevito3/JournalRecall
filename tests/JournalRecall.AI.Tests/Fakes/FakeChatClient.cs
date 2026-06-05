using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IChatClient"/> for runner tests. Returns scripted responses turn by
/// turn; once the script is exhausted it returns a final text answer. Scripts may throw to simulate
/// transient faults.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly Queue<Func<int, ChatResponse>> _script = new();

    public int CallCount { get; private set; }
    public List<IReadOnlyList<ChatMessage>> ReceivedMessages { get; } = [];

    /// <summary>Scripts a turn that returns a final assistant text answer (no tool calls).</summary>
    public FakeChatClient RespondsWithText(string text, long totalTokens = 10)
    {
        _script.Enqueue(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { TotalTokenCount = totalTokens },
        });
        return this;
    }

    /// <summary>Scripts a turn that requests a tool call.</summary>
    public FakeChatClient RequestsTool(string name, IDictionary<string, object?>? args = null, long totalTokens = 10, string? callId = null)
    {
        _script.Enqueue(call =>
        {
            var content = new FunctionCallContent(callId ?? $"call-{call}", name, args);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [content]))
            {
                Usage = new UsageDetails { TotalTokenCount = totalTokens },
            };
        });
        return this;
    }

    /// <summary>Scripts a turn that throws (transient fault simulation).</summary>
    public FakeChatClient Throws(Exception exception)
    {
        _script.Enqueue(_ => throw exception);
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        ReceivedMessages.Add(messages.ToArray());

        var handler = _script.Count > 0
            ? _script.Dequeue()
            : _ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"))
            {
                Usage = new UsageDetails { TotalTokenCount = 1 },
            };

        return Task.FromResult(handler(CallCount));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Reuse the same scripted turn, projected to updates — lets the fake serve MCP sampling
        // (which streams) as well as the runner (which uses GetResponseAsync).
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
            yield return new ChatResponseUpdate(message.Role, message.Contents);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
