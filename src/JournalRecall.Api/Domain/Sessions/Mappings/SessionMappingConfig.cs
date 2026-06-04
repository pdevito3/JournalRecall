using Mapster;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Mappings;

public sealed class SessionMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config) => config.NewConfig<Session, SessionDto>();
}
