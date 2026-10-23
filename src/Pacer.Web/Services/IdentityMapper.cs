using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Pacer.Web.Services;

public sealed record SessionIdentity(string Sub, string Name, string? Email);

/// <summary>Turns the claims Authentik sends into the small identity Pacer keeps in its session cookie.</summary>
public static class IdentityMapper
{
    public static SessionIdentity? Map(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub)) return null;

        var email = Clean(principal.FindFirst("email")?.Value);
        var name = Clean(principal.FindFirst("name")?.Value)
                   ?? Clean(principal.FindFirst("preferred_username")?.Value)
                   ?? email
                   ?? sub;

        return new SessionIdentity(sub, name, email);
    }

    public static ClaimsPrincipal ToPrincipal(SessionIdentity identity)
    {
        var claims = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, "name", ClaimTypes.Role);
        claims.AddClaim(new Claim("sub", identity.Sub));
        claims.AddClaim(new Claim("name", identity.Name));
        return new ClaimsPrincipal(claims);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
