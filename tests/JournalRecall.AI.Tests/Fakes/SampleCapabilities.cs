using System.ComponentModel;
using JournalRecall.AI.Core;

namespace JournalRecall.AI.Tests.Fakes;

internal sealed class GreetTool : ITool
{
    public static string Name => "greet";
    public static string? Description => "Greet a person by name.";
    public static string? Scope => "people:greet";

    public Delegate Handler => ([Description("The person's name")] string name) => $"Hello, {name}!";
}

internal sealed class GuidelinesResource : IResource
{
    public static string Name => "guidelines";
    public static string? Description => "Dietary guidelines.";
    public static string? Scope => null;

    public Task<ResourceContent> ReadAsync(RunContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new ResourceContent("Eat more vegetables."));
}

internal sealed class SecretResource : IResource
{
    public static string Name => "secret";
    public static string? Description => "Sensitive data.";
    public static string? Scope => "secret:read";

    public Task<ResourceContent> ReadAsync(RunContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new ResourceContent("the answer is 42"));
}

internal sealed class TonePrompt : IPrompt
{
    public static string Name => "tone";

    public string Render(RunContext context) => "Always answer politely.";
}

internal sealed class EchoAgent : IAgent
{
    public static string Name => "echoer";

    public static void Configure(IAgentBuilder builder) =>
        builder.UsingModel(RunnerHarness.ModelName).WithInstructions("Echo the input.").WithMaxTurns(3);
}
