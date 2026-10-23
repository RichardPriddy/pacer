using Microsoft.AspNetCore.Components.Authorization;

namespace Pacer.Web.Services;

public interface ICurrentUser
{
    /// <summary>The signed-in user's OIDC subject.</summary>
    Task<string> GetSubAsync();
}

public sealed class CurrentUser(AuthenticationStateProvider auth) : ICurrentUser
{
    public async Task<string> GetSubAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        return state.User.Sub();
    }
}
