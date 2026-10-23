using System.Security.Claims;
using Pacer.Web.Services;

namespace Pacer.Web.Tests.Services;

public class UserExtensionsTests
{
    [Fact]
    public void Sub_returns_the_sub_claim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "abc-123")], "test"));

        Assert.Equal("abc-123", user.Sub());
    }

    [Fact]
    public void Sub_throws_when_the_claim_is_missing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "Ann")], "test"));

        Assert.Throws<InvalidOperationException>(() => user.Sub());
    }

    [Fact]
    public void Sub_throws_for_an_anonymous_user()
    {
        Assert.Throws<InvalidOperationException>(() => new ClaimsPrincipal(new ClaimsIdentity()).Sub());
    }
}
