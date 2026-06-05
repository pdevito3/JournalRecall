namespace JournalRecall.AI.SourceGeneration;

/// <summary>Well-known names from the JournalRecall.AI contract the generator/analyzer reason about.</summary>
internal static class AgentContract
{
    public const string AgentInterface = "JournalRecall.AI.Core.IAgent";
    public const string ToolInterface = "JournalRecall.AI.Core.ITool";
    public const string ConfigureMethod = "Configure";
    public const string NameProperty = "Name";
    public const string UsingModelMethod = "UsingModel";
    public const string GeneratedNamespace = "JournalRecall.AI.Generated";
}
