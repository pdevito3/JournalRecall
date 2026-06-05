using System.Text.Json.Serialization;

namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// How a piece of Session metadata came to be (CONTEXT.md): the user set it, or the AI suggested it
/// (and it was accepted). AI never overwrites <see cref="UserSet"/> metadata. Serialized by name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetadataProvenance>))]
public enum MetadataProvenance
{
    UserSet,
    AiSuggested,
}
