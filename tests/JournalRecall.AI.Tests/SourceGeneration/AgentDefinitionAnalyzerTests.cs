using Shouldly;

namespace JournalRecall.AI.Tests.SourceGeneration;

public class AgentDefinitionAnalyzerTests
{
    private const string Contract = """
        namespace JournalRecall.AI.Core
        {
            public interface IAgentBuilder { IAgentBuilder UsingModel(string m); IAgentBuilder WithMaxTurns(int n); }
            public interface IAgent { static abstract string Name { get; } static abstract void Configure(IAgentBuilder b); }
        }
        """;

    [Fact]
    public async Task Well_formed_agent_produces_no_diagnostics()
    {
        var source = Contract + """
            namespace Demo
            {
                public class GoodAgent : JournalRecall.AI.Core.IAgent
                {
                    public static string Name => "good";
                    public static void Configure(JournalRecall.AI.Core.IAgentBuilder b) => b.UsingModel("fast");
                }
            }
            """;

        var diagnostics = await RoslynHarness.RunAnalyzerAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Agent_without_UsingModel_reports_PW0002()
    {
        var source = Contract + """
            namespace Demo
            {
                public class NoModelAgent : JournalRecall.AI.Core.IAgent
                {
                    public static string Name => "nomodel";
                    public static void Configure(JournalRecall.AI.Core.IAgentBuilder b) => b.WithMaxTurns(3);
                }
            }
            """;

        var diagnostics = await RoslynHarness.RunAnalyzerAsync(source);

        diagnostics.Where(d => d.Id == "PW0002").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Duplicate_agent_names_report_PW0001()
    {
        var source = Contract + """
            namespace Demo
            {
                public class A : JournalRecall.AI.Core.IAgent
                {
                    public static string Name => "dup";
                    public static void Configure(JournalRecall.AI.Core.IAgentBuilder b) => b.UsingModel("x");
                }
                public class B : JournalRecall.AI.Core.IAgent
                {
                    public static string Name => "dup";
                    public static void Configure(JournalRecall.AI.Core.IAgentBuilder b) => b.UsingModel("x");
                }
            }
            """;

        var diagnostics = await RoslynHarness.RunAnalyzerAsync(source);

        diagnostics.Where(d => d.Id == "PW0001").Count().ShouldBe(2); // one per declaration site
    }
}
