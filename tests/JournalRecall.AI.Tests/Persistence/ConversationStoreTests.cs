using Shouldly;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core.Persistence;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.EntityFrameworkCore;

namespace JournalRecall.AI.Tests.Persistence;

/// <summary>
/// Store contract tests run against both the in-memory default and the EF Core (SQLite) satellite,
/// proving the shared StoreBase OCC/idempotency orchestration (ADR-0007).
/// </summary>
public class ConversationStoreTests
{
    public static IEnumerable<object[]> Stores()
    {
        yield return ["in-memory"];
        yield return ["ef-sqlite"];
    }

    private static (IConversationStore store, IDisposable? owner) Build(string kind)
    {
        if (kind == "in-memory")
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddJournalRecallAgents();
            var provider = services.BuildServiceProvider();
            return (provider.GetRequiredService<IConversationStore>(), provider);
        }

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var efServices = new ServiceCollection();
        efServices.AddLogging();
        efServices.AddJournalRecallAgents().WithEfCoreConversations(o => o.UseSqlite(connection));
        var efProvider = efServices.BuildServiceProvider();
        using (var scope = efProvider.CreateScope())
            scope.ServiceProvider.GetRequiredService<ConversationDbContext>().Database.EnsureCreated();
        return (efProvider.GetRequiredService<IConversationStore>(),
            new CompositeDisposable(efProvider, connection));
    }

    private static StoredMessage Msg(string role, string text) =>
        new() { Role = role, Text = text, CreatedAt = DateTimeOffset.UnixEpoch };

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Append_then_load_round_trips_messages_and_version(string kind)
    {
        var (store, owner) = Build(kind);
        using var _ = owner;
        var threadId = $"t-{kind}-roundtrip";

        var v1 = await store.AppendAsync(threadId, [Msg("user", "hi"), Msg("assistant", "hello")], expectedVersion: 0);
        var thread = await store.LoadAsync(threadId);

        v1.ShouldBe(1);
        thread.Version.ShouldBe(1);
        thread.Messages.Select(m => m.Text).ShouldBe(["hi", "hello"]);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Stale_expected_version_throws_concurrency_exception(string kind)
    {
        var (store, owner) = Build(kind);
        using var _ = owner;
        var threadId = $"t-{kind}-occ";

        await store.AppendAsync(threadId, [Msg("user", "first")], expectedVersion: 0);

        // A second writer that still thinks the thread is at version 0.
        var act = () => store.AppendAsync(threadId, [Msg("user", "stale")], expectedVersion: 0);

        await Should.ThrowAsync<ConversationConcurrencyException>(act);
        (await store.LoadAsync(threadId)).Messages.ShouldHaveSingleItem();
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Duplicate_idempotency_key_is_a_no_op(string kind)
    {
        var (store, owner) = Build(kind);
        using var _ = owner;
        var threadId = $"t-{kind}-idem";

        var v1 = await store.AppendAsync(threadId, [Msg("user", "send once")], expectedVersion: 0, idempotencyKey: "k1");
        // Client retry: same key, same expected version.
        var v2 = await store.AppendAsync(threadId, [Msg("user", "send once")], expectedVersion: 0, idempotencyKey: "k1");

        v1.ShouldBe(1);
        v2.ShouldBe(1); // duplicate returns the current version, no new write
        (await store.LoadAsync(threadId)).Messages.ShouldHaveSingleItem();
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var d in disposables) d.Dispose();
        }
    }
}
