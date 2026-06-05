using Shouldly;

namespace JournalRecall.AI.Tests.SourceGeneration;

public class AgentCatalogGeneratorTests
{
    private const string TwoAgents = """
        namespace JournalRecall.AI.Core { public interface IAgent {} }
        namespace Demo
        {
            public class FooAgent : JournalRecall.AI.Core.IAgent {}
            public class BarAgent : JournalRecall.AI.Core.IAgent {}
        }
        """;

    [Fact]
    public void Emits_catalog_and_registration_for_each_agent()
    {
        var generated = RoslynHarness.RunGenerator(TwoAgents);

        generated.ShouldContain("class GeneratedAgentCatalog : global::JournalRecall.AI.Core.IAgentCatalog");
        generated.ShouldContain("AddGeneratedAgents");
        generated.ShouldContain("global::Demo.FooAgent.Configure(builder)");
        generated.ShouldContain("global::Demo.BarAgent.Configure(builder)");
    }

    [Fact]
    public void Abstract_and_non_agent_types_are_ignored()
    {
        const string source = """
            namespace JournalRecall.AI.Core { public interface IAgent {} }
            namespace Demo
            {
                public abstract class AbstractAgent : JournalRecall.AI.Core.IAgent {}
                public class NotAnAgent {}
            }
            """;

        var generated = RoslynHarness.RunGenerator(source);

        generated.ShouldNotContain("AbstractAgent");
        generated.ShouldNotContain("NotAnAgent");
    }

    [Fact]
    public void Emits_an_empty_catalog_when_no_agents_exist()
    {
        var generated = RoslynHarness.RunGenerator("namespace JournalRecall.AI.Core { public interface IAgent {} public interface ITool {} }");

        // Still additive: the extensions compile for inline-only consumers.
        generated.ShouldContain("AddGeneratedAgents");
        generated.ShouldNotContain("Build_0()");
    }

    [Fact]
    public void Emits_tool_registration_for_each_ITool()
    {
        const string source = """
            namespace JournalRecall.AI.Core { public interface IAgent {} public interface ITool {} }
            namespace Demo
            {
                public class GreetTool : JournalRecall.AI.Core.ITool {}
                public class AddTool : JournalRecall.AI.Core.ITool {}
            }
            """;

        var generated = RoslynHarness.RunGenerator(source);

        generated.ShouldContain("AddGeneratedTools");
        generated.ShouldContain("AddScoped<global::Demo.GreetTool>");
        generated.ShouldContain("AddScoped<global::Demo.AddTool>");
    }
}
