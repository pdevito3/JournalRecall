using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core;
using JournalRecall.AI.Core.Persistence;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Persistence;

public class ConversationThreadRunnerTests
{
    private static (ConversationThreadRunner threadRunner, IConversationStore store) Build(FakeChatClient client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, client);
        services.AddJournalRecallAgents();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<ConversationThreadRunner>(),
            provider.GetRequiredService<IConversationStore>());
    }

    private static AgentDefinition Def() =>
        Agent.Define("assistant").UsingModel(RunnerHarness.ModelName).WithInstructions("be brief").WithMaxTurns(3).Build();

    [Fact]
    public async Task Persists_user_and_assistant_turns_and_replays_history_on_the_next_run()
    {
        var client = new FakeChatClient().RespondsWithText("first answer").RespondsWithText("second answer");
        var (threadRunner, store) = Build(client);
        const string threadId = "thread-1";

        await threadRunner.RunAsync(Def(), threadId, "first question", new RunContext());
        await threadRunner.RunAsync(Def(), threadId, "second question", new RunContext());

        // The system prompt is run-scoped context, never persisted; only the conversation turns are.
        var thread = await store.LoadAsync(threadId);
        thread.Messages.Select(m => m.Text)
            .ShouldBe(["first question", "first answer", "second question", "second answer"]);
        thread.Version.ShouldBe(2);

        // The second model call must have seen the prior turns replayed (history + new question).
        var secondCall = client.ReceivedMessages[1];
        secondCall.ShouldContain(m => m.Text == "first question");
        secondCall.ShouldContain(m => m.Text == "first answer");
    }

    [Fact]
    public async Task Idempotency_key_makes_a_retried_turn_safe()
    {
        var client = new FakeChatClient().RespondsWithText("answer").RespondsWithText("should not append");
        var (threadRunner, store) = Build(client);
        const string threadId = "thread-2";

        await threadRunner.RunAsync(Def(), threadId, "q", new RunContext(), idempotencyKey: "req-1");
        await threadRunner.RunAsync(Def(), threadId, "q", new RunContext(), idempotencyKey: "req-1");

        (await store.LoadAsync(threadId)).Messages.Count().ShouldBe(2); // not duplicated
    }

    [Fact]
    public async Task StreamAsync_yields_lifecycle_events_and_persists_the_turn()
    {
        var client = new FakeChatClient().RespondsWithText("streamed answer");
        var (threadRunner, store) = Build(client);
        const string threadId = "thread-stream-1";

        var events = new List<AgentEvent>();
        await foreach (var e in threadRunner.StreamAsync(Def(), threadId, "stream question", new RunContext()))
            events.Add(e);

        // The stream surfaces the lifecycle and ends on a terminal event.
        events[0].ShouldBeOfType<AgentEvent.RunStarted>();
        events[^1].ShouldBeOfType<AgentEvent.Completed>();

        // The turn is persisted just like the non-streaming path.
        (await store.LoadAsync(threadId)).Messages.Select(m => m.Text)
            .ShouldBe(["stream question", "streamed answer"]);
    }

    [Fact]
    public async Task StreamAsync_replays_prior_history_on_the_next_turn()
    {
        var client = new FakeChatClient().RespondsWithText("first answer").RespondsWithText("second answer");
        var (threadRunner, store) = Build(client);
        const string threadId = "thread-stream-2";

        await Drain(threadRunner.StreamAsync(Def(), threadId, "first question", new RunContext()));
        await Drain(threadRunner.StreamAsync(Def(), threadId, "second question", new RunContext()));

        var thread = await store.LoadAsync(threadId);
        thread.Messages.Select(m => m.Text)
            .ShouldBe(["first question", "first answer", "second question", "second answer"]);
        thread.Version.ShouldBe(2);

        // The second model call saw the prior turns replayed.
        var secondCall = client.ReceivedMessages[1];
        secondCall.ShouldContain(m => m.Text == "first question");
        secondCall.ShouldContain(m => m.Text == "first answer");
    }

    [Fact]
    public async Task StreamAsync_idempotency_key_makes_a_retried_turn_safe()
    {
        var client = new FakeChatClient().RespondsWithText("answer").RespondsWithText("should not append");
        var (threadRunner, store) = Build(client);
        const string threadId = "thread-stream-3";

        await Drain(threadRunner.StreamAsync(Def(), threadId, "q", new RunContext(), idempotencyKey: "req-1"));
        await Drain(threadRunner.StreamAsync(Def(), threadId, "q", new RunContext(), idempotencyKey: "req-1"));

        (await store.LoadAsync(threadId)).Messages.Count().ShouldBe(2); // not duplicated
    }

    private static async Task Drain(IAsyncEnumerable<AgentEvent> events)
    {
        await foreach (var _ in events) { }
    }
}
