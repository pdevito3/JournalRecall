using Microsoft.EntityFrameworkCore;
using JournalRecall.AI.OpenAI;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Domain.Admin.Services;

/// <summary>
/// Resolves the chat-model options actually in effect (issue 0016): the Admin-configured app-wide AI
/// provider when one is set, otherwise the appsettings <c>ChatModels:*</c> fallback the app booted with.
/// The single stored config drives every logical model (Cleanup, Summary). Read fresh each call so an
/// Admin change takes effect on the next run without a restart.
/// </summary>
public sealed class EffectiveChatModelOptions(JournalRecallDbContext db)
{
    public async Task<ChatModelOptions> ResolveAsync(ChatModelOptions fallback, CancellationToken cancellationToken = default)
    {
        var stored = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (stored is null || !stored.IsConfigured)
            return fallback;

        return new ChatModelOptions
        {
            Provider = stored.Provider,
            Endpoint = stored.Endpoint,
            ApiKey = stored.ApiKey,
            Model = stored.Model,
        };
    }
}
