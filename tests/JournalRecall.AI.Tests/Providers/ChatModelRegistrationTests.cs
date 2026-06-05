using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.OpenAI;
using Shouldly;

namespace JournalRecall.AI.Tests.Providers;

/// <summary>
/// The provider satellite resolves a keyed <see cref="IChatClient"/> for each logical model. Resolving
/// forces the concrete client to be constructed, which also pins down that the Azure.AI.OpenAI and
/// OpenAI SDK versions are runtime-compatible (no network calls are made by construction).
/// </summary>
public class ChatModelRegistrationTests
{
    [Fact]
    public void OpenAI_compatible_model_resolves_as_chat_client()
    {
        using var provider = Build(b => b.AddChatModel("fast", o =>
        {
            o.Provider = ChatProvider.OpenAI;
            o.Endpoint = "http://localhost:11434/v1";
            o.ApiKey = "ollama";
            o.Model = "qwen2.5:7b-instruct";
        }));

        provider.GetRequiredKeyedService<IChatClient>("fast").ShouldNotBeNull();
    }

    [Fact]
    public void Azure_openai_model_resolves_as_chat_client()
    {
        using var provider = Build(b => b.AddChatModel("smart", o =>
        {
            o.Provider = ChatProvider.AzureOpenAI;
            o.Endpoint = "https://example.openai.azure.com";
            o.ApiKey = "secret";
            o.Model = "gpt-4o"; // deployment name
        }));

        provider.GetRequiredKeyedService<IChatClient>("smart").ShouldNotBeNull();
    }

    [Fact]
    public void Model_binds_from_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ChatModels:fast:Provider"] = "OpenAI",
                ["ChatModels:fast:Endpoint"] = "http://localhost:11434/v1",
                ["ChatModels:fast:ApiKey"] = "ollama",
                ["ChatModels:fast:Model"] = "qwen2.5:7b-instruct",
            })
            .Build();

        using var provider = Build(b => b.AddChatModel("fast", configuration.GetSection("ChatModels:fast")));

        provider.GetRequiredKeyedService<IChatClient>("fast").ShouldNotBeNull();
    }

    [Fact]
    public void Azure_without_endpoint_throws_on_resolve()
    {
        using var provider = Build(b => b.AddChatModel("smart", o =>
        {
            o.Provider = ChatProvider.AzureOpenAI;
            o.ApiKey = "secret";
            o.Model = "gpt-4o";
        }));

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredKeyedService<IChatClient>("smart"));
    }

    private static ServiceProvider Build(Action<IJournalRecallAgentsBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services.AddJournalRecallAgents());
        return services.BuildServiceProvider();
    }
}
