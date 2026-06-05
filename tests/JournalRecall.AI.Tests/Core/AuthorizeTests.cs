using Shouldly;
using JournalRecall.AI.Core;
using static JournalRecall.AI.Tests.Core.StateFactory;

namespace JournalRecall.AI.Tests.Core;

public class AuthorizeTests
{
    private static AgentState StateWith(AgentDefinition def, params string[] scopes) =>
        State(def, context: WithScopes(scopes));

    [Fact]
    public void Allows_tool_with_no_scope()
    {
        var def = Agent.Define("a").WithTool("ping").Build();
        AgentPolicy.Authorize(new ToolInvocation("ping"), State(def)).IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void Allows_tool_when_caller_holds_scope()
    {
        var def = Agent.Define("a").WithTool("write", scope: "recipes:write").Build();
        var result = AgentPolicy.Authorize(new ToolInvocation("write"), StateWith(def, "recipes:write"));
        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void Denies_tool_when_caller_lacks_scope()
    {
        var def = Agent.Define("a").WithTool("write", scope: "recipes:write").Build();
        var result = AgentPolicy.Authorize(new ToolInvocation("write"), StateWith(def, "recipes:read"));
        result.ShouldBeOfType<AuthorizationResult.Denied>();
    }

    [Fact]
    public void Denies_unknown_tool()
    {
        var def = Agent.Define("a").WithTool("known").Build();
        var result = AgentPolicy.Authorize(new ToolInvocation("mystery"), State(def));
        result.ShouldBeOfType<AuthorizationResult.Denied>()
            .Reason.ShouldContain("Unknown tool");
    }

    [Fact]
    public void Authorizes_sub_agent_delegation_with_same_scope_mechanism()
    {
        var def = Agent.Define("a").CanCall("meal-planner", scope: "mealplans:write").Build();

        AgentPolicy.Authorize(new ToolInvocation("meal-planner"), StateWith(def, "mealplans:write"))
            .IsAllowed.ShouldBeTrue();
        AgentPolicy.Authorize(new ToolInvocation("meal-planner"), State(def))
            .ShouldBeOfType<AuthorizationResult.Denied>();
    }
}
