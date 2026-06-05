namespace JournalRecall.AI.Core;

/// <summary>Entry point for inline agent authoring: <c>Agent.Define("name").UsingModel(...)...Build()</c>.</summary>
public static class Agent
{
    /// <summary>Begins building an agent definition with the given name.</summary>
    public static IAgentBuilder Define(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AgentBuilder(name);
    }
}

/// <summary>Mutable builder that accumulates configuration and emits an immutable <see cref="AgentDefinition"/>.</summary>
internal sealed class AgentBuilder(string name) : IAgentBuilder
{
    private readonly string _name = name;
    private string? _model;
    private string? _instructions;
    private readonly List<ToolDescriptor> _tools = [];
    private readonly List<ResourceDescriptor> _resources = [];
    private readonly List<PromptDescriptor> _prompts = [];
    private readonly List<SubAgentDescriptor> _subAgents = [];
    private readonly List<McpServerRef> _mcpServers = [];
    private int? _maxTurns;
    private TimeSpan? _maxDuration;
    private long? _tokenBudget;

    public IAgentBuilder UsingModel(string logicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        _model = logicalName;
        return this;
    }

    public IAgentBuilder WithInstructions(string instructions)
    {
        _instructions = instructions;
        return this;
    }

    public IAgentBuilder WithTool(string name, string? scope = null, int maxRetries = 2, bool failClosed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        _tools.Add(new ToolDescriptor
        {
            Name = name,
            Scope = scope,
            MaxRetries = maxRetries,
            FailClosed = failClosed,
        });
        return this;
    }

    public IAgentBuilder WithTool<T>(int maxRetries = 2, bool failClosed = false) where T : ITool
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(T.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        _tools.Add(new ToolDescriptor
        {
            Name = T.Name,
            Description = T.Description,
            Scope = T.Scope,
            MaxRetries = maxRetries,
            FailClosed = failClosed,
            ImplementationType = typeof(T),
        });
        return this;
    }

    public IAgentBuilder WithResource(string name, ResourceMode mode, string? scope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _resources.Add(new ResourceDescriptor { Name = name, Mode = mode, Scope = scope });
        return this;
    }

    public IAgentBuilder WithResource<T>(ResourceMode mode) where T : IResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(T.Name);
        _resources.Add(new ResourceDescriptor
        {
            Name = T.Name,
            Mode = mode,
            Description = T.Description,
            Scope = T.Scope,
            ImplementationType = typeof(T),
        });
        return this;
    }

    public IAgentBuilder WithPrompt(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _prompts.Add(new PromptDescriptor { Name = name });
        return this;
    }

    public IAgentBuilder WithPrompt<T>() where T : IPrompt
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(T.Name);
        _prompts.Add(new PromptDescriptor { Name = T.Name, ImplementationType = typeof(T) });
        return this;
    }

    public IAgentBuilder WithMcpServer(string serverName, string? scope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        _mcpServers.Add(new McpServerRef { Name = serverName, Scope = scope });
        return this;
    }

    public IAgentBuilder CanCall(string agentName, string? scope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        _subAgents.Add(new SubAgentDescriptor { Name = agentName, Scope = scope });
        return this;
    }

    public IAgentBuilder CanCall<T>(string? scope = null) where T : IAgent
    {
        var subBuilder = new AgentBuilder(T.Name);
        T.Configure(subBuilder);
        _subAgents.Add(new SubAgentDescriptor
        {
            Name = T.Name,
            Scope = scope,
            AgentType = typeof(T),
            Definition = subBuilder.Build(),
        });
        return this;
    }

    public IAgentBuilder WithMaxTurns(int maxTurns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        _maxTurns = maxTurns;
        return this;
    }

    public IAgentBuilder WithDeadline(TimeSpan maxDuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDuration.Ticks);
        _maxDuration = maxDuration;
        return this;
    }

    public IAgentBuilder WithTokenBudget(long tokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokens);
        _tokenBudget = tokens;
        return this;
    }

    public AgentDefinition Build()
    {
        var tools = new List<ToolDescriptor>(_tools);

        // Discoverable resources are surfaced through synthetic tools; declare them so the pure
        // Authorize recognizes the calls (ADR-0003). They are unscoped — read_resource enforces each
        // resource's own scope at fetch time.
        if (_resources.Any(r => r.Mode == ResourceMode.Discoverable))
        {
            if (tools.All(t => t.Name != SyntheticTools.ListResources))
                tools.Add(new ToolDescriptor { Name = SyntheticTools.ListResources });
            if (tools.All(t => t.Name != SyntheticTools.ReadResource))
                tools.Add(new ToolDescriptor { Name = SyntheticTools.ReadResource });
        }

        return new AgentDefinition
        {
            Name = _name,
            ModelName = _model,
            Instructions = _instructions,
            Tools = tools.ToArray(),
            Resources = _resources.ToArray(),
            Prompts = _prompts.ToArray(),
            SubAgents = _subAgents.ToArray(),
            McpServers = _mcpServers.ToArray(),
            MaxTurns = _maxTurns,
            MaxDuration = _maxDuration,
            TokenBudget = _tokenBudget,
        };
    }
}

/// <summary>Reserved tool names the framework synthesizes for discoverable resources (ADR-0003).</summary>
public static class SyntheticTools
{
    public const string ListResources = "list_resources";
    public const string ReadResource = "read_resource";
}
