using System.Diagnostics;
using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Observability;

/// <summary>
/// The EF query tags reach the trace. A query that carries <c>TagWithOperationCallSite</c> must export a
/// database span named after the operation and carrying its call site, so a slow query in a trace points
/// at the feature slice that issued it instead of a bare table name.
/// </summary>
public class query_tag_telemetry_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task a_tagged_query_exports_a_named_span_with_its_call_site()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var dto = await created.ReadJsonAsync<SessionDto>();
        FakeAuth.ExportedActivities.Clear();

        var get = await client.GetAsync(ApiRoutes.Sessions.Get(dto!.Id));

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var span = FakeAuth.ExportedActivities
            .Where(a => a.DisplayName == "SQL: sessions.get_by_id")
            .ShouldHaveSingleItem();
        span.GetTagItem("db.operation.name").ShouldBe("sessions.get_by_id");
        span.GetTagItem("code.function").ShouldBe("Handle");
        (span.GetTagItem("code.file.path") as string).ShouldEndWith("GetSession.cs");
        span.GetTagItem("code.line.number").ShouldNotBeNull();
        (span.GetTagItem("journalrecall.db.call_site") as string).ShouldNotBeNull()
            .ShouldContain("GetSession.cs:");
    }

    [Fact]
    public async Task an_untagged_query_keeps_the_default_span_name()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        FakeAuth.ExportedActivities.Clear();

        await client.PostAsync(ApiRoutes.Sessions.Create(), null);

        // Database spans are the only Client-kind spans on this path.
        var dbSpans = FakeAuth.ExportedActivities.Where(a => a.Kind == ActivityKind.Client).ToList();
        dbSpans.ShouldContain(a => a.DisplayName == "SQL: sessions.create.location_opt_in");
        // The insert and the Identity lookups carry no tag, so the enrichment leaves them alone.
        dbSpans.ShouldContain(a => !a.DisplayName.StartsWith("SQL: "));
    }
}
