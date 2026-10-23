using System.Security.Claims;

namespace Pacer.Web.Services;

public static class UserExtensions
{
    /// <summary>The OIDC subject. Every database query is scoped by this value.</summary>
    public static string Sub(this ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("The signed-in user has no 'sub' claim.");
}
