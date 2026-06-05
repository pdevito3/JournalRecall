using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using JournalRecall.AI.Core;
using JournalRecall.AI.Runtime.Mcp;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// The default capability resolver. Materializes local <see cref="ITool"/>s into authorized
/// <see cref="AIFunction"/>s, assembles <see cref="IResource"/> content (pinned now / discoverable via
/// synthetic tools), renders <see cref="IPrompt"/>s, compiles <c>.CanCall</c> delegation, and pulls
/// tools/resources from external MCP servers — all unified at the <see cref="AIFunction"/> layer
/// (ADR-0003/0004). MCP tools (already <see cref="AIFunction"/>s) flow straight in.
/// </summary>
internal sealed class DiCapabilityResolver : ICapabilityResolver
{
    public async Task<MaterializedCapabilities> ResolveAsync(
        AgentDefinition definition,
        RunContext context,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var tools = new List<AITool>();
        var pinned = new List<ChatMessage>();
        var discoverable = new List<DiscoverableResource>();
        var extraDescriptors = new List<ToolDescriptor>();
        var progress = new System.Collections.Concurrent.ConcurrentQueue<AgentEvent>();

        AddPrompts(definition, context, scopedServices, pinned);
        AddLocalTools(definition, scopedServices, tools);
        await AddLocalResourcesAsync(definition, context, scopedServices, pinned, discoverable, cancellationToken);
        await AddMcpCapabilitiesAsync(definition, scopedServices, tools, discoverable, extraDescriptors, progress, cancellationToken);
        AddSyntheticResourceTools(definition, context, discoverable, tools, extraDescriptors);
        AddDelegations(definition, context, scopedServices, tools);

        return new MaterializedCapabilities
        {
            Tools = tools,
            PinnedContext = pinned,
            ExtraToolDescriptors = extraDescriptors,
            ProgressEvents = progress,
        };
    }

    private static void AddPrompts(
        AgentDefinition definition, RunContext context, IServiceProvider services, List<ChatMessage> pinned)
    {
        foreach (var prompt in definition.Prompts)
        {
            if (prompt.ImplementationType is null) continue;
            var instance = (IPrompt)services.GetRequiredService(prompt.ImplementationType);
            pinned.Add(new ChatMessage(ChatRole.System, instance.Render(context)));
        }
    }

    private static void AddLocalTools(AgentDefinition definition, IServiceProvider services, List<AITool> tools)
    {
        foreach (var descriptor in definition.Tools)
        {
            if (descriptor.ImplementationType is null) continue; // synthetic/MCP tools handled elsewhere
            var tool = (ITool)services.GetRequiredService(descriptor.ImplementationType);
            tools.Add(AIFunctionFactory.Create(
                tool.Handler,
                new AIFunctionFactoryOptions { Name = descriptor.Name, Description = descriptor.Description }));
        }
    }

    private static async Task AddLocalResourcesAsync(
        AgentDefinition definition,
        RunContext context,
        IServiceProvider services,
        List<ChatMessage> pinned,
        List<DiscoverableResource> discoverable,
        CancellationToken cancellationToken)
    {
        foreach (var descriptor in definition.Resources)
        {
            if (descriptor.ImplementationType is null) continue;
            var resource = (IResource)services.GetRequiredService(descriptor.ImplementationType);

            if (descriptor.Mode == ResourceMode.Pinned)
            {
                var content = await resource.ReadAsync(context, cancellationToken);
                pinned.Add(new ChatMessage(ChatRole.System, $"[resource:{descriptor.Name}]\n{content.Text}"));
            }
            else
            {
                discoverable.Add(new DiscoverableResource(
                    descriptor.Name, descriptor.Description, descriptor.Scope,
                    async (ctx, ct) => (await resource.ReadAsync(ctx, ct)).Text));
            }
        }
    }

    private static async Task AddMcpCapabilitiesAsync(
        AgentDefinition definition,
        IServiceProvider services,
        List<AITool> tools,
        List<DiscoverableResource> discoverable,
        List<ToolDescriptor> extraDescriptors,
        System.Collections.Concurrent.ConcurrentQueue<AgentEvent> progress,
        CancellationToken cancellationToken)
    {
        if (definition.McpServers.Count == 0)
            return;

        var provider = services.GetRequiredService<IMcpClientProvider>();
        var progressForwarder = new McpProgressForwarder(progress);

        foreach (var server in definition.McpServers)
        {
            var client = await provider.GetClientAsync(server.Name, cancellationToken);
            var capabilities = client.ServerCapabilities;

            // MCP tools already are AIFunctions; they flow straight in, authorized at server
            // granularity, and wrapped to forward server progress notifications to our event stream.
            if (capabilities?.Tools is not null)
            {
                foreach (var tool in await client.ListToolsAsync(cancellationToken: cancellationToken))
                {
                    tools.Add(tool.WithProgress(progressForwarder));
                    extraDescriptors.Add(new ToolDescriptor { Name = tool.Name, Description = tool.Description, Scope = server.Scope });
                }
            }

            // MCP resources are surfaced as discoverable (app-controlled delivery, ADR-0003).
            if (capabilities?.Resources is not null)
            {
                foreach (var resource in await client.ListResourcesAsync(cancellationToken: cancellationToken))
                {
                    var uri = resource.Uri;
                    discoverable.Add(new DiscoverableResource(
                        resource.Name, resource.Description, server.Scope,
                        async (_, ct) => ExtractText(await client.ReadResourceAsync(uri, cancellationToken: ct))));
                }
            }
        }
    }

    private static void AddSyntheticResourceTools(
        AgentDefinition definition,
        RunContext context,
        List<DiscoverableResource> discoverable,
        List<AITool> tools,
        List<ToolDescriptor> extraDescriptors)
    {
        if (discoverable.Count == 0)
            return;

        tools.Add(AIFunctionFactory.Create(
            () => JsonSerializer.Serialize(discoverable.Select(d => new { name = d.Name, description = d.Description })),
            new AIFunctionFactoryOptions
            {
                Name = SyntheticTools.ListResources,
                Description = "List the resources that can be fetched with read_resource.",
            }));

        var read = (Func<string, Task<string>>)(async name =>
        {
            var match = discoverable.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
            if (match is null)
                return $"No discoverable resource named '{name}'.";
            if (!context.HasScope(match.Scope))
                return $"Authorization denied: missing scope '{match.Scope}'.";
            return await match.Read(context, CancellationToken.None);
        });

        tools.Add(AIFunctionFactory.Create(read, new AIFunctionFactoryOptions
        {
            Name = SyntheticTools.ReadResource,
            Description = "Read the content of a named resource.",
        }));

        // Discoverable resources from MCP (or any runtime source) need their synthetic tools authorized
        // even when the definition declared no local discoverable resource at Build time.
        foreach (var name in new[] { SyntheticTools.ListResources, SyntheticTools.ReadResource })
            if (definition.FindTool(name) is null && extraDescriptors.All(d => d.Name != name))
                extraDescriptors.Add(new ToolDescriptor { Name = name });
    }

    private static void AddDelegations(
        AgentDefinition definition, RunContext context, IServiceProvider services, List<AITool> tools)
    {
        foreach (var subAgent in definition.SubAgents)
        {
            if (subAgent.Definition is null) continue; // by-name delegation needs the Phase 9 catalog

            var subDefinition = subAgent.Definition;
            var delegateCall = (Func<string, CancellationToken, Task<string>>)(async (input, ct) =>
            {
                var runner = services.GetRequiredService<IAgentRunner>();
                var outcome = await runner.RunAsync(subDefinition, Conversation.FromUser(input), context, ct);
                return Summarize(outcome);
            });

            tools.Add(AIFunctionFactory.Create(delegateCall, new AIFunctionFactoryOptions
            {
                Name = subAgent.Name,
                Description = $"Delegate the task to the '{subAgent.Name}' agent and return its answer.",
            }));
        }
    }

    private static string ExtractText(ReadResourceResult result) =>
        string.Join("\n", result.Contents.OfType<TextResourceContents>().Select(c => c.Text));

    private static string Summarize(AgentOutcome outcome) => outcome switch
    {
        AgentOutcome.Completed c => c.Messages.LastOrDefault()?.Text ?? string.Empty,
        AgentOutcome.Stopped s => BuildPartial($"[delegate stopped: {s.Reason}]", s.Messages),
        AgentOutcome.Failed f => $"[delegate failed: {f.Reason}]",
        _ => string.Empty,
    };

    private static string BuildPartial(string prefix, IReadOnlyList<ChatMessage> messages)
    {
        var sb = new StringBuilder(prefix);
        var last = messages.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text;
        if (!string.IsNullOrEmpty(last))
            sb.Append(' ').Append(last);
        return sb.ToString();
    }

    private sealed record DiscoverableResource(
        string Name, string? Description, string? Scope, Func<RunContext, CancellationToken, Task<string>> Read);

    /// <summary>Forwards MCP server progress notifications onto the agent event stream.</summary>
    private sealed class McpProgressForwarder(System.Collections.Concurrent.ConcurrentQueue<AgentEvent> sink)
        : IProgress<ProgressNotificationValue>
    {
        public void Report(ProgressNotificationValue value) =>
            sink.Enqueue(new AgentEvent.Progress(value.Progress, value.Total, value.Message));
    }
}
