using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using JournalRecall.AI.DependencyInjection;

namespace JournalRecall.AI.OpenAI;

/// <summary>
/// Registers chat models backed by OpenAI-shaped providers, the provider satellite to
/// <c>JournalRecall.AI</c>. The agent's logical model name (e.g. "fast") is the DI key; the provider is
/// chosen internally from <see cref="ChatModelOptions.Provider"/> — OpenAI-compatible or Azure OpenAI.
/// The core library stays provider-agnostic (it only ever sees <see cref="IChatClient"/>).
/// </summary>
public static class OpenAIChatModelExtensions
{
    /// <summary>Registers a keyed chat model configured imperatively.</summary>
    public static IJournalRecallAgentsBuilder AddChatModel(
        this IJournalRecallAgentsBuilder builder, string key, Action<ChatModelOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ChatModelOptions();
        configure(options);
        return builder.AddChatModel(key, options);
    }

    /// <summary>Registers a keyed chat model bound from a configuration section (e.g. <c>ChatModels:fast</c>).</summary>
    public static IJournalRecallAgentsBuilder AddChatModel(
        this IJournalRecallAgentsBuilder builder, string key, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new ChatModelOptions();
        configuration.Bind(options);
        return builder.AddChatModel(key, options);
    }

    private static IJournalRecallAgentsBuilder AddChatModel(
        this IJournalRecallAgentsBuilder builder, string key, ChatModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Capture the resolved options now; the client itself is built lazily on first resolve.
        builder.Services.AddKeyedSingleton<IChatClient>(key, (_, _) => CreateChatClient(options));
        return builder;
    }

    /// <summary>
    /// Builds an <see cref="IChatClient"/> from options directly — for callers that resolve provider
    /// settings at runtime (e.g. an Admin-configured app-wide provider) rather than at startup.
    /// </summary>
    public static IChatClient CreateChatClient(ChatModelOptions options)
    {
        switch (options.Provider)
        {
            case ChatProvider.AzureOpenAI:
            {
                if (string.IsNullOrWhiteSpace(options.Endpoint))
                    throw new InvalidOperationException("Azure OpenAI requires an Endpoint.");
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                    throw new InvalidOperationException("Azure OpenAI requires an ApiKey.");

                var azure = new AzureOpenAIClient(new Uri(options.Endpoint), new ApiKeyCredential(options.ApiKey));
                return azure.GetChatClient(options.Model).AsIChatClient();
            }

            case ChatProvider.OpenAI:
            default:
            {
                var clientOptions = new OpenAIClientOptions();
                if (!string.IsNullOrWhiteSpace(options.Endpoint))
                    clientOptions.Endpoint = new Uri(options.Endpoint);

                // Local OpenAI-compatible servers ignore the key but the SDK requires a non-empty credential.
                var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(options.ApiKey) ? "not-needed" : options.ApiKey);
                return new OpenAIClient(credential, clientOptions).GetChatClient(options.Model).AsIChatClient();
            }
        }
    }
}
