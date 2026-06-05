using Mapster;
using JournalRecall.Api.Domain.Corrections.Dtos;

namespace JournalRecall.Api.Domain.Corrections.Mappings;

public sealed class CorrectionMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config) => config.NewConfig<Correction, CorrectionDto>();
}
