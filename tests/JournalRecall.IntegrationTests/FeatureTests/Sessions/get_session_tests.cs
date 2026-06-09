using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Integration reference test (PRD-0003, TEST-0003): the Privacy invariant at the data layer — one User
/// can never read another User's Session. Each <see cref="TestingServiceScope"/> is a distinct User, so
/// cross-User denial falls out of the per-User tenant filter with no reset between tests.
/// </summary>
public class get_session_tests : TestBase
{
    [Fact]
    public async Task a_user_can_read_their_own_session()
    {
        using var scope = new TestingServiceScope();
        var created = await scope.SendAsync(new CreateSession.Command(null, null));

        var view = await scope.SendAsync(new GetSession.Query(created!.Id));

        view.ShouldNotBeNull();
        view!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task a_user_cannot_read_another_users_session()
    {
        using var alice = new TestingServiceScope();
        var aliceSession = await alice.SendAsync(new CreateSession.Command(null, null));

        using var bob = new TestingServiceScope();
        var bobView = await bob.SendAsync(new GetSession.Query(aliceSession!.Id));

        bobView.ShouldBeNull(); // never another User's content (Privacy invariant)
    }
}
