using System.ClientModel;
using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;

namespace JournalRecall.AI.Tests.Runtime;

/// <summary>
/// End-to-end smoke test against a real local model (Ollama, OpenAI-compatible endpoint). Gated by
/// the JOURNALRECALL_OLLAMA=1 env var so it never runs in the deterministic CI gate (ADR-0008).
/// </summary>
public class OllamaEndToEndTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static bool Enabled => Environment.GetEnvironmentVariable("JOURNALRECALL_OLLAMA") == "1";

    [Fact]
    public async Task Agent_runs_against_local_qwen_model()
    {
        if (!Enabled)
            return; // skipped outside the opt-in local run

        var openai = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") });
        IChatClient chat = openai.GetChatClient("qwen2.5:7b-instruct").AsIChatClient();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton("fast", chat);
        services.AddJournalRecallAgents();
        var runner = services.BuildServiceProvider().GetRequiredService<IAgentRunner>();

        var def = Agent.Define("assistant")
            .UsingModel("fast")
            .WithInstructions("You are a terse assistant. Answer in one short sentence.")
            .WithMaxTurns(3)
            .Build();

        var outcome = await runner.RunAsync(
            def,
            Conversation.FromUser("Name one macronutrient found in chicken breast."),
            new RunContext());

        var completed = outcome.ShouldBeOfType<AgentOutcome.Completed>();
        var answer = completed.Messages.Last().Text;
        answer.ShouldNotBeNullOrWhiteSpace();
        output.WriteLine($"Model answered: {answer}");
    }
}
