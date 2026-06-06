using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Services;

namespace JournalRecall.UnitTests.Services;

/// <summary>
/// Pure test: <see cref="ICurrentUserService"/> projects id, username, and roles straight off the
/// validated principal. No host or DB — a <see cref="HttpContextAccessor"/> is constructed directly.
/// </summary>
public class current_user_service_tests
{
    [Fact]
    public void projects_id_username_and_roles_from_the_principal()
    {
        var id = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
                new Claim(JwtRegisteredClaimNames.PreferredUsername, "admin"),
                new Claim(ClaimTypes.Role, Roles.Admin),
            ], authenticationType: "test")),
        };

        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        service.UserId.ShouldBe(id);
        service.UserName.ShouldBe("admin");
        service.Roles.ShouldContain(Roles.Admin);
        service.IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public void returns_empty_identity_when_unauthenticated()
    {
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        service.UserId.ShouldBeNull();
        service.Roles.ShouldBeEmpty();
        service.IsAdmin.ShouldBeFalse();
    }
}
