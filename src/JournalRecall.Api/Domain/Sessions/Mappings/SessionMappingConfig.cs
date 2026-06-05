using Mapster;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Mappings;

public sealed class SessionMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config) =>
        // Surface the effective status (Stale derived), not the stored coarse one.
        config.NewConfig<Session, SessionDto>()
            .Map(dest => dest.CleanupStatus, src => src.EffectiveCleanupStatus);
}
