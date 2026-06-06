using Microsoft.Extensions.DependencyInjection;

namespace JournalRecall.FunctionalTests;

/// <summary>
/// The single serial functional collection (PRD-0003). Owns one real-auth host and one fake-auth host for
/// the whole assembly; the fake-auth scheme lives only in <see cref="FakeAuthWebApplicationFactory"/>.
/// </summary>
public sealed class WebTestFixture : IAsyncLifetime
{
    public TestingWebApplicationFactory RealAuth { get; private set; } = null!;
    public FakeAuthWebApplicationFactory FakeAuth { get; private set; } = null!;

    public Task InitializeAsync()
    {
        RealAuth = new TestingWebApplicationFactory();
        FakeAuth = new FakeAuthWebApplicationFactory();
        // Touch Services to boot each host and run its startup migrations + seeders before any test runs.
        _ = RealAuth.Services.GetRequiredService<IServiceScopeFactory>();
        _ = FakeAuth.Services.GetRequiredService<IServiceScopeFactory>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await RealAuth.DisposeAsync();
        await FakeAuth.DisposeAsync();
    }
}

[CollectionDefinition(nameof(WebTestFixture))]
public sealed class WebTestFixtureCollection : ICollectionFixture<WebTestFixture>;

/// <summary>Base for functional test classes: exposes the two hosts via the shared collection fixture.</summary>
[Collection(nameof(WebTestFixture))]
public abstract class TestBase(WebTestFixture fixture)
{
    protected TestingWebApplicationFactory RealAuth => fixture.RealAuth;
    protected FakeAuthWebApplicationFactory FakeAuth => fixture.FakeAuth;
}
