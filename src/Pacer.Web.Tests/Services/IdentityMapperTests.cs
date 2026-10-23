using System.Security.Claims;
using Pacer.Web.Services;

namespace Pacer.Web.Tests.Services;

public class IdentityMapperTests
{
    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Principal_without_a_sub_is_rejected()
    {
        Assert.Null(IdentityMapper.Map(Principal(("name", "Ann"))));
    }

    [Fact]
    public void Blank_sub_is_rejected()
    {
        Assert.Null(IdentityMapper.Map(Principal(("sub", "  "), ("name", "Ann"))));
    }

    [Fact]
    public void Name_claim_is_preferred()
    {
        var id = IdentityMapper.Map(Principal(("sub", "u1"), ("name", "Ann Lee"), ("preferred_username", "ann"), ("email", "ann@example.com")));

        Assert.Equal("u1", id!.Sub);
        Assert.Equal("Ann Lee", id.Name);
        Assert.Equal("ann@example.com", id.Email);
    }

    [Fact]
    public void Preferred_username_is_used_when_there_is_no_name()
    {
        var id = IdentityMapper.Map(Principal(("sub", "u1"), ("preferred_username", "ann"), ("email", "ann@example.com")));

        Assert.Equal("ann", id!.Name);
    }

    [Fact]
    public void Email_is_used_when_there_is_no_name_or_username()
    {
        var id = IdentityMapper.Map(Principal(("sub", "u1"), ("email", "ann@example.com")));

        Assert.Equal("ann@example.com", id!.Name);
    }

    [Fact]
    public void Sub_is_the_last_resort_name()
    {
        var id = IdentityMapper.Map(Principal(("sub", "u1")));

        Assert.Equal("u1", id!.Name);
        Assert.Null(id.Email);
    }

    [Fact]
    public void Blank_claims_are_ignored_and_values_are_trimmed()
    {
        var id = IdentityMapper.Map(Principal(("sub", "u1"), ("name", "   "), ("preferred_username", "  ann  "), ("email", " ")));

        Assert.Equal("ann", id!.Name);
        Assert.Null(id.Email);
    }

    [Fact]
    public void Session_principal_carries_only_sub_and_name()
    {
        var principal = IdentityMapper.ToPrincipal(new SessionIdentity("u1", "Ann", "ann@example.com"));

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("Cookies", principal.Identity.AuthenticationType);
        Assert.Equal("Ann", principal.Identity.Name);
        Assert.Equal("u1", principal.Sub());
        Assert.Equal(2, principal.Claims.Count());
    }

    [Fact]
    public void Mapped_identity_round_trips_through_the_session_principal()
    {
        var id = IdentityMapper.Map(Principal(("sub", "u1"), ("name", "Ann")))!;

        Assert.Equal("u1", IdentityMapper.ToPrincipal(id).Sub());
    }
}
