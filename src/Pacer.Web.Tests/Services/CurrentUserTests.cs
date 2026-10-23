using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Pacer.Web.Services;

namespace Pacer.Web.Tests.Services;

public class CurrentUserTests
{
    private sealed class StubAuthProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    [Fact]
    public async Task Returns_the_sub_of_the_signed_in_user()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "test"));
        var current = new CurrentUser(new StubAuthProvider(principal));

        Assert.Equal("user-1", await current.GetSubAsync());
    }

    [Fact]
    public async Task Throws_when_nobody_is_signed_in()
    {
        var current = new CurrentUser(new StubAuthProvider(new ClaimsPrincipal(new ClaimsIdentity())));

        await Assert.ThrowsAsync<InvalidOperationException>(() => current.GetSubAsync());
    }
}
