using Shouldly;
using JournalRecall.AI.Core;

namespace JournalRecall.AI.Tests.Core;

public class AgentBuilderTests
{
    [Fact]
    public void Build_captures_all_fluent_configuration()
    {
        var def = Agent.Define("nutritionist")
            .UsingModel("reasoning")
            .WithInstructions("You are a nutritionist.")
            .WithTool("get_recipe_macros", scope: "recipes:read")
            .WithTool("add_recipe", scope: "recipes:write", maxRetries: 1, failClosed: true)
            .WithResource("dietary-guidelines", ResourceMode.Pinned)
            .WithResource("usda-db", ResourceMode.Discoverable, scope: "nutrition:read")
            .WithPrompt("clinical-tone")
            .CanCall("meal-planner", scope: "mealplans:write")
            .WithMaxTurns(8)
            .WithDeadline(TimeSpan.FromSeconds(30))
            .WithTokenBudget(50_000)
            .Build();

        def.Name.ShouldBe("nutritionist");
        def.ModelName.ShouldBe("reasoning");
        def.Instructions.ShouldBe("You are a nutritionist.");
        def.Tools.ShouldContain(t => t.Name == "get_recipe_macros");
        var addRecipe = def.FindTool("add_recipe").ShouldNotBeNull();
        addRecipe.Scope.ShouldBe("recipes:write");
        addRecipe.MaxRetries.ShouldBe(1);
        addRecipe.FailClosed.ShouldBeTrue();
        // The discoverable resource synthesizes list/read_resource tool descriptors (ADR-0003).
        var toolNames = def.Tools.Select(t => t.Name).ToList();
        toolNames.ShouldContain(SyntheticTools.ListResources);
        toolNames.ShouldContain(SyntheticTools.ReadResource);
        def.Resources.Count().ShouldBe(2);
        def.Resources.ShouldContain(r => r.Name == "usda-db" && r.Mode == ResourceMode.Discoverable);
        def.Prompts.Where(p => p.Name == "clinical-tone").ShouldHaveSingleItem();
        def.SubAgents.Where(s => s.Name == "meal-planner" && s.Scope == "mealplans:write").ShouldHaveSingleItem();
        def.MaxTurns.ShouldBe(8);
        def.MaxDuration.ShouldBe(TimeSpan.FromSeconds(30));
        def.TokenBudget.ShouldBe(50_000);
    }

    [Fact]
    public void Build_is_pure_and_repeatable()
    {
        var builder = Agent.Define("a").UsingModel("fast").WithTool("t");

        var first = builder.Build();
        var second = builder.Build();

        first.ShouldBeEquivalentTo(second);
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void Tool_defaults_to_two_retries_and_not_fail_closed()
    {
        var tool = Agent.Define("a").WithTool("t").Build().FindTool("t")!;

        tool.MaxRetries.ShouldBe(2);
        tool.FailClosed.ShouldBeFalse();
        tool.Scope.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Define_rejects_blank_name(string name) =>
        Should.Throw<ArgumentException>(() => Agent.Define(name));

    [Fact]
    public void WithMaxTurns_rejects_non_positive() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Agent.Define("a").WithMaxTurns(0));
}
