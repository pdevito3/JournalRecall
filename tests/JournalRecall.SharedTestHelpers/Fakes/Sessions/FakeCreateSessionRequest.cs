using Soenneker.Utils.AutoBogus;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.SharedTestHelpers.Fakes.Sessions;

/// <summary>
/// Generates a <see cref="CreateSession.Request"/> via AutoFaker (Soenneker) with realistic, in-range
/// coordinates (PRD-0003). Constructed explicitly so the positional record is bound correctly; a test
/// can still override fields with <c>.RuleFor(...)</c> on an instance.
/// </summary>
public sealed class FakeCreateSessionRequest : AutoFaker<CreateSession.Request>
{
    public FakeCreateSessionRequest()
    {
        CustomInstantiator(f => new CreateSession.Request(null, f.Address.Latitude(), f.Address.Longitude()));
    }
}
