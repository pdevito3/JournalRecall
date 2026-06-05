using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Runtime;

public class CapabilityResolverTests
{
    private static async Task<MaterializedCapabilities> ResolveAsync(
        AgentDefinition definition, RunContext? context = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, new FakeChatClient());
        services.AddJournalRecallAgents()
            .AddTool<GreetTool>()
            .AddResource<GuidelinesResource>()
            .AddResource<SecretResource>()
            .AddPrompt<TonePrompt>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = provider.GetRequiredService<ICapabilityResolver>();
        return await resolver.ResolveAsync(definition, context ?? new RunContext(), scope.ServiceProvider, default);
    }

    [Fact]
    public async Task Tool_is_adapted_to_an_AIFunction_with_correct_schema()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithTool<GreetTool>().Build();

        var caps = await ResolveAsync(def);

        var function = caps.Tools.OfType<AIFunction>().Single();
        function.Name.ShouldBe("greet");
        function.Description.ShouldBe("Greet a person by name.");
        function.JsonSchema.ToString().ShouldContain("name");
    }

    [Fact]
    public async Task Pinned_resource_content_is_injected_into_context()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithResource<GuidelinesResource>(ResourceMode.Pinned).Build();

        var caps = await ResolveAsync(def);

        caps.PinnedContext.ShouldHaveSingleItem()
            .Text.ShouldContain("Eat more vegetables.");
        caps.Tools.ShouldBeEmpty(); // pinned resources are not surfaced as tools
    }

    [Fact]
    public async Task Discoverable_resource_surfaces_list_and_read_tools_but_no_pinned_content()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithResource<GuidelinesResource>(ResourceMode.Discoverable).Build();

        var caps = await ResolveAsync(def);

        caps.PinnedContext.ShouldBeEmpty();
        var names = caps.Tools.OfType<AIFunction>().Select(t => t.Name).ToList();
        names.ShouldContain(SyntheticTools.ListResources);
        names.ShouldContain(SyntheticTools.ReadResource);
    }

    [Fact]
    public async Task Read_resource_returns_content_for_unscoped_resource()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithResource<GuidelinesResource>(ResourceMode.Discoverable).Build();
        var caps = await ResolveAsync(def);
        var read = caps.Tools.OfType<AIFunction>().Single(t => t.Name == SyntheticTools.ReadResource);

        var result = await read.InvokeAsync(new AIFunctionArguments { ["name"] = "guidelines" });

        result.ShouldNotBeNull().ToString()!.ShouldContain("Eat more vegetables.");
    }

    [Fact]
    public async Task Read_resource_denies_when_caller_lacks_the_resource_scope()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithResource<SecretResource>(ResourceMode.Discoverable).Build();
        var caps = await ResolveAsync(def); // caller has no scopes
        var read = caps.Tools.OfType<AIFunction>().Single(t => t.Name == SyntheticTools.ReadResource);

        var result = await read.InvokeAsync(new AIFunctionArguments { ["name"] = "secret" });

        result.ShouldNotBeNull().ToString()!.ShouldContain("Authorization denied");
    }

    [Fact]
    public async Task Read_resource_returns_content_when_caller_holds_scope()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithResource<SecretResource>(ResourceMode.Discoverable).Build();
        var ctx = new RunContext { Scopes = new HashSet<string> { "secret:read" } };
        var caps = await ResolveAsync(def, ctx);
        var read = caps.Tools.OfType<AIFunction>().Single(t => t.Name == SyntheticTools.ReadResource);

        var result = await read.InvokeAsync(new AIFunctionArguments { ["name"] = "secret" });

        result.ShouldNotBeNull().ToString()!.ShouldContain("42");
    }

    [Fact]
    public async Task Prompt_is_rendered_into_context()
    {
        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithPrompt<TonePrompt>().Build();

        var caps = await ResolveAsync(def);

        caps.PinnedContext.ShouldHaveSingleItem().Text.ShouldBe("Always answer politely.");
    }
}
