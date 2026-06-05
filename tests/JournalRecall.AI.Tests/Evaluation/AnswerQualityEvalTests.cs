using System.ClientModel;
using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;

namespace JournalRecall.AI.Tests.Evaluation;

/// <summary>
/// Opt-in agent answer-quality evals against a real model (Ollama). Env-gated by JOURNALRECALL_EVAL=1 so
/// they are excluded from the deterministic CI gate (ADR-0008). Uses M.E.AI.Evaluation quality
/// evaluators (the model also acts as the judge).
/// </summary>
public class AnswerQualityEvalTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static bool Enabled => Environment.GetEnvironmentVariable("JOURNALRECALL_EVAL") == "1";

    private static IChatClient Ollama() =>
        new OpenAIClient(new ApiKeyCredential("ollama"),
                new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") })
            .GetChatClient("qwen2.5:7b-instruct").AsIChatClient();

    [Fact]
    public async Task Nutrition_answer_is_relevant_to_the_question()
    {
        if (!Enabled)
            return; // skipped outside the opt-in local/nightly eval run

        var chat = Ollama();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton("fast", chat);
        services.AddJournalRecallAgents();
        var runner = services.BuildServiceProvider().GetRequiredService<IAgentRunner>();

        const string question = "Which macronutrient is most abundant in chicken breast?";
        var definition = Agent.Define("nutritionist")
            .UsingModel("fast")
            .WithInstructions("You are a dietitian. Answer in one concise sentence.")
            .WithMaxTurns(2)
            .Build();

        var outcome = await runner.RunAsync(definition, Conversation.FromUser(question), new RunContext());
        var answer = outcome.ShouldBeOfType<AgentOutcome.Completed>().Messages.Last().Text;
        output.WriteLine($"Answer: {answer}");

        // The model also judges quality (relevance on a 1-5 scale).
        var evaluator = new RelevanceEvaluator();
        var configuration = new ChatConfiguration(chat);
        var result = await evaluator.EvaluateAsync(
            [new ChatMessage(ChatRole.User, question)],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)),
            configuration);

        var relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);
        output.WriteLine($"Relevance: {relevance.Value} ({relevance.Reason})");
        var score = relevance.Value.ShouldNotBeNull();
        score.ShouldBeGreaterThanOrEqualTo(3.0);
    }
}
