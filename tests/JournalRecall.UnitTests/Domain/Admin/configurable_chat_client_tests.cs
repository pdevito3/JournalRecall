using Microsoft.Extensions.AI;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Domain.Admin.Services;

namespace JournalRecall.UnitTests.Domain.Admin;

/// <summary>
/// Pure test for the in-flight-safe config rotation in <see cref="ConfigurableChatClient"/> (issue 0031):
/// a Cleanup/Summary call runs outside the resolve lock, so a provider-config swap mid-call must not dispose
/// the client the call is still awaiting. Driven through the internal test seam (resolve + create delegates)
/// so no DI scope, DB, or real provider is needed.
/// </summary>
public class configurable_chat_client_tests
{
    /// <summary>An inner client that blocks inside <see cref="GetResponseAsync"/> until released, and throws if used after disposal.</summary>
    private sealed class BlockingChatClient(string reply) : IChatClient
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }
        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();
            await _release.Task;
            ObjectDisposedException.ThrowIf(Disposed, this);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, reply));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task swap_during_in_flight_call_does_not_dispose_the_client_it_is_awaiting()
    {
        var first = new BlockingChatClient("from-first");
        var second = new BlockingChatClient("from-second");
        second.Release(); // the second client doesn't need to block

        // The "stored config" the resolver returns. Flipping it forces the next resolve to swap clients.
        var options = new ChatModelOptions { Model = "model-a" };
        var created = new Queue<IChatClient>([first, second]);

        var client = new ConfigurableChatClient(
            _ => Task.FromResult(options),
            _ => created.Dequeue());

        // Begin a request that blocks mid-call against the first client.
        var inFlight = client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);
        await first.Entered;

        // Rotate the provider config while the first call is parked. The next resolve builds the second
        // client and would, pre-fix, synchronously dispose the first — out from under the parked call.
        options.Model = "model-b";
        var afterSwap = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "again")]);
        afterSwap.Text.ShouldBe("from-second"); // a request after the swap uses the new config

        first.Disposed.ShouldBeFalse(); // the still-in-flight call kept the old client alive

        // Now let the pre-swap call finish: it must succeed (no ObjectDisposedException).
        first.Release();
        var beforeSwap = await inFlight;
        beforeSwap.Text.ShouldBe("from-first");

        // Once the last holder released it, the superseded client is disposed.
        first.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task dispose_during_in_flight_call_defers_disposal_until_the_call_completes()
    {
        var inner = new BlockingChatClient("ok");
        var created = new Queue<IChatClient>([inner]);
        var client = new ConfigurableChatClient(
            _ => Task.FromResult(new ChatModelOptions { Model = "model-a" }),
            _ => created.Dequeue());

        var inFlight = client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);
        await inner.Entered;

        client.Dispose();
        inner.Disposed.ShouldBeFalse(); // not disposed while a call still holds it

        inner.Release();
        var response = await inFlight; // completes without ObjectDisposedException
        response.Text.ShouldBe("ok");
        inner.Disposed.ShouldBeTrue();
    }
}
