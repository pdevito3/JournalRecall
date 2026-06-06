using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Audit fields (BaseEntity) at the integration layer: the DbContext stamps CreatedAt/CreatedBy on insert
/// and UpdatedAt/UpdatedBy on every save, from the request's clock and current User — without the domain
/// setting them. Driven by the controllable <see cref="TestFixture.Clock"/>.
/// </summary>
public class audit_fields_tests : TestBase
{
    private static Task<Session> Load(TestingServiceScope scope, Guid id) =>
        scope.ExecuteDbContextAsync(db =>
            db.Sessions.IgnoreQueryFilters().AsNoTracking().FirstAsync(s => s.Id == id));

    [Fact]
    public async Task insert_stamps_creation_and_modification_from_the_clock_and_current_user()
    {
        var start = Clock.GetUtcNow();
        using var scope = new TestingServiceScope();

        var dto = await scope.SendAsync(new CreateSession.Command(null, null));

        var session = await Load(scope, dto.Id);
        session.CreatedAt.ShouldBe(start);
        session.UpdatedAt.ShouldBe(start);
        session.CreatedBy.ShouldBe(session.UserId);   // the creator stamped as the author
        session.UpdatedBy.ShouldBe(session.UserId);
    }

    [Fact]
    public async Task update_advances_only_the_modification_fields()
    {
        using var scope = new TestingServiceScope();
        var dto = await scope.SendAsync(new CreateSession.Command(null, null));
        var atCreate = await Load(scope, dto.Id);

        Clock.Advance(TimeSpan.FromMinutes(5));
        (await scope.SendAsync(new SaveDraft.Command(dto.Id, "a later edit"))).ShouldBeTrue();

        var afterEdit = await Load(scope, dto.Id);
        afterEdit.CreatedAt.ShouldBe(atCreate.CreatedAt);               // creation is immutable
        afterEdit.CreatedBy.ShouldBe(atCreate.CreatedBy);
        afterEdit.UpdatedAt.ShouldBe(atCreate.CreatedAt.AddMinutes(5)); // modification advanced
        afterEdit.UpdatedBy.ShouldBe(afterEdit.UserId);
    }
}
