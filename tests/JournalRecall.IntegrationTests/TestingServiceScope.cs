using System.Security.Claims;
using HeimGuard;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using JournalRecall.Api.Databases;
using JournalRecall.SharedTestHelpers.Fakes.Identity;

namespace JournalRecall.IntegrationTests;

/// <summary>
/// A DI scope that acts as a single fresh <b>User</b> (the isolation boundary — ADR-0006). On
/// construction it mints a random User, seeds the row directly (bypassing UserManager/Identity), and
/// places a matching <see cref="ClaimsPrincipal"/> on the mocked accessor so <c>ICurrentUserService</c>
/// (and the construction-time DbContext tenant filter) resolve identity exactly as in production. HeimGuard
/// is permitted-by-default. Surface: <see cref="GetService{T}"/>, <see cref="SendAsync{TResponse}"/>,
/// <see cref="InsertAsync{T}"/>, <see cref="FindAsync{T}"/>, <see cref="ExecuteDbContextAsync{T}"/>,
/// <see cref="SetUser"/>/<see cref="AsAdmin"/>/<see cref="SetUserNotPermitted"/>, and
/// <see cref="CurrentUserId"/>.
/// </summary>
public sealed class TestingServiceScope : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IHttpContextAccessor _accessor;
    private readonly IHeimGuardClient _heimGuard;
    private readonly string _email;

    /// <summary>The id of this scope's User — the tenant every query and write is scoped to.</summary>
    public Guid CurrentUserId { get; }

    public TestingServiceScope()
    {
        _scope = TestFixture.ScopeFactory.CreateScope();
        // Both are singletons; resolving via the scope returns the shared instances the host owns.
        _accessor = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        _heimGuard = _scope.ServiceProvider.GetRequiredService<IHeimGuardClient>();

        CurrentUserId = Guid.CreateVersion7();
        _email = $"user-{CurrentUserId:N}@example.com";
        SetUser(FakeClaimsPrincipal.ForUser(CurrentUserId, _email));
        SetUserIsPermitted();

        // Seed the fresh User row. Resolving the scoped DbContext here binds its tenant id to this User
        // (the principal is already set), so every later query in this scope is isolated to it.
        var db = _scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        db.Users.Add(new FakeUserBuilder().WithId(CurrentUserId).WithEmail(_email).Build());
        db.SaveChanges();
    }

    public TScopedService GetService<TScopedService>() where TScopedService : notnull =>
        _scope.ServiceProvider.GetRequiredService<TScopedService>();

    /// <summary>Places a principal on the mocked accessor (the live identity for this scope's requests).</summary>
    public void SetUser(ClaimsPrincipal user) =>
        _accessor.HttpContext = new DefaultHttpContext { User = user };

    /// <summary>Re-issues this scope's User as an Admin (same id, plus the Admin role claim).</summary>
    public TestingServiceScope AsAdmin()
    {
        SetUser(FakeClaimsPrincipal.ForAdmin(CurrentUserId, _email));
        return this;
    }

    public void SetUserIsPermitted() =>
        _heimGuard.HasPermissionAsync(Arg.Any<string>()).Returns(true);

    /// <summary>Denies a specific permission so authorization-failure paths are testable.</summary>
    public void SetUserNotPermitted(string permission) =>
        _heimGuard.HasPermissionAsync(permission).Returns(false);

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request) =>
        _scope.ServiceProvider.GetRequiredService<ISender>().Send(request);

    public Task SendAsync(IRequest request) =>
        _scope.ServiceProvider.GetRequiredService<ISender>().Send(request);

    public async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues) where TEntity : class =>
        await _scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>().FindAsync<TEntity>(keyValues);

    public async Task InsertAsync<TEntity>(params TEntity[] entities) where TEntity : class
    {
        var db = _scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        foreach (var entity in entities)
            db.Add(entity);
        await db.SaveChangesAsync();
    }

    public Task ExecuteDbContextAsync(Func<JournalRecallDbContext, Task> action) =>
        action(_scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>());

    public Task<T> ExecuteDbContextAsync<T>(Func<JournalRecallDbContext, Task<T>> action) =>
        action(_scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>());

    public void Dispose() => _scope.Dispose();
}
