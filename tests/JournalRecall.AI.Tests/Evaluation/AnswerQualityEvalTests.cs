using System.ClientModel;
using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using JournalRecall.Api.Domain.Sessions.Ai;

namespace JournalRecall.AI.Tests.Evaluation;

/// <summary>
/// Opt-in answer-quality eval for the real Cleanup agent against a live model (Ollama). Env-gated by
/// JOURNALRECALL_EVAL=1 so it is excluded from the deterministic CI gate (ADR-0008). When the flag is
/// unset the test is SKIPPED (via SkippableFact), never reported as an assertion-free pass.
/// </summary>
public class AnswerQualityEvalTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static bool Enabled => Environment.GetEnvironmentVariable("JOURNALRECALL_EVAL") == "1";

    private static IChatClient Ollama() =>
        new OpenAIClient(new ApiKeyCredential("ollama"),
                new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") })
            .GetChatClient("qwen2.5:7b-instruct").AsIChatClient();

    [SkippableFact]
    public async Task Cleanup_synopsis_is_first_person_and_preserves_the_entry()
    {
        Skip.IfNot(Enabled, "Set JOURNALRECALL_EVAL=1 to run the opt-in answer-quality eval against a live model.");

        var chat = Ollama();
        var services = new ServiceCollection();
        services.AddLogging();
        // The Cleanup agent resolves its IChatClient from the keyed "cleanup" model slot.
        services.AddKeyedSingleton(CleanupAgent.ModelKey, chat);
        services.AddJournalRecallAgents();
        var runner = services.BuildServiceProvider().GetRequiredService<IAgentRunner>();

        const string raw =
            "had coffee w/ sarah this mroning before work. we talked about the move to portland and " +
            "wether the new job is worth it. felt anxious but also kind of excited about it.";

        var definition = CleanupAgent.BuildDefinition();
        var outcome = await runner.RunAsync(definition, Conversation.FromUser(raw), new RunContext());
        var completed = outcome.ShouldBeOfType<AgentOutcome.Completed>();

        CleanupAgent.TryParse(completed, out var parsed).ShouldBeTrue("Cleanup agent did not return parseable JSON.");
        output.WriteLine($"Cleaned:\n{parsed.CleanedMarkdown}\n\nSynopsis: {parsed.Synopsis}");

        // Quality properties of a good Cleanup result:
        // 1. The synopsis is a non-empty first-person recap ("I ...").
        parsed.Synopsis.ShouldNotBeNullOrWhiteSpace();
        parsed.Synopsis.ShouldContain("I", Case.Sensitive);

        // 2. The cleaned copy preserves the entry's content (Sarah is still referenced) and fixes the
        //    obvious dictation typo ("mroning" -> "morning"), without dropping the topic.
        parsed.CleanedMarkdown.ShouldNotBeNullOrWhiteSpace();
        parsed.CleanedMarkdown.ShouldContain("Sarah", Case.Insensitive);
        parsed.CleanedMarkdown.ShouldNotContain("mroning", Case.Insensitive);

        // 3. The people-proposal side-channel surfaces the tagged person.
        parsed.PeopleProposal.ShouldContain(p => p.Contains("Sarah", StringComparison.OrdinalIgnoreCase));
    }
}
