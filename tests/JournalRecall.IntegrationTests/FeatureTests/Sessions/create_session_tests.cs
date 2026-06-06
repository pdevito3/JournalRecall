using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Integration reference test (PRD-0003, TEST-0003): CreateSession sent through the real MediatR slice
/// against real SQLite, with no HTTP, persists a Session scoped to the current User.
/// </summary>
public class create_session_tests : TestBase
{
    [Fact]
    public async Task create_session_persists_and_scopes_to_the_current_user()
    {
        using var scope = new TestingServiceScope();

        var dto = await scope.SendAsync(new CreateSession.Command(null, null));

        dto.ShouldNotBeNull();
        dto.CleanupStatus.ShouldBe(CleanupStatus.NotRun);

        // The persisted row is visible through the tenant-scoped DbContext and owned by this User.
        var persisted = await scope.ExecuteDbContextAsync(db =>
            db.Sessions.FirstOrDefaultAsync(s => s.Id == dto.Id));
        persisted.ShouldNotBeNull();
        persisted!.UserId.ShouldBe(scope.CurrentUserId);
    }
}
