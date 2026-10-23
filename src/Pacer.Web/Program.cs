using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Pacer.Web.Components;
using Pacer.Web.Data;
using Pacer.Web.Services;
using System.Net;

var settings = AppSettings.Load();
var builder = WebApplication.CreateBuilder(args); 

builder.Services.AddSingleton(settings);
builder.Services.AddDbContextFactory<PacerDbContext>(o => o.UseNpgsql(ConnectionStrings.Postgres(settings)));
builder.Services.AddSingleton<ICounterStore, EfCounterStore>();
builder.Services.AddSingleton<IClock>(new Clock(settings.Timezone));
builder.Services.AddScoped<CounterService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();


// Data protection keys live in the database so all replicas share them.
builder.Services.AddDataProtection().SetApplicationName("pacer");
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<IDbContextFactory<PacerDbContext>>((options, factory) => options.XmlRepository = new EfXmlRepository(factory));

// Cookie policy
var cookiePolicy = settings.CookieSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "pacer_csrf";
    o.Cookie.SecurePolicy = cookiePolicy;
});

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(o =>
    {
        o.Cookie.Name = "pacer_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = cookiePolicy;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    })
    .AddOpenIdConnect(o =>
    {
        o.Authority = settings.OidcIssuer;
        o.ClientId = settings.OidcClientId;
        o.ClientSecret = settings.OidcClientSecret;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.ResponseMode = OpenIdConnectResponseMode.Query; // lets the SameSite=Lax cookies below work
        o.UsePkce = true;
        o.CallbackPath = "/auth/openid/callback";
        o.SignedOutCallbackPath = "/auth/openid/signout-callback";
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.GetClaimsFromUserInfoEndpoint = true;
        o.SaveTokens = false;
        o.MapInboundClaims = false; // keep claim names such as "sub" and "name" as the provider sends them

        o.Scope.Clear();
        foreach (var scope in settings.OidcScopes) o.Scope.Add(scope);

        o.CorrelationCookie.SameSite = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = cookiePolicy;
        o.NonceCookie.SameSite = SameSiteMode.Lax;
        o.NonceCookie.SecurePolicy = cookiePolicy;

        o.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                // Pin the redirect URI to the public URL when configured.
                if (!string.IsNullOrEmpty(settings.BaseUrl))
                {
                    var redirect = settings.BaseUrl + o.CallbackPath;
                    ctx.ProtocolMessage.RedirectUri = redirect;
                    ctx.Properties.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey] = redirect;
                }
                return Task.CompletedTask;
            },

            OnTicketReceived = async ctx =>
            {
                var identity = IdentityMapper.Map(ctx.Principal!);
                if (identity is null)
                {
                    ctx.Fail("The identity provider did not return a user id.");
                    return;
                }
                
                var store = ctx.HttpContext.RequestServices.GetRequiredService<ICounterStore>();
                await store.UpsertUserAsync(identity.Sub, identity.Name, identity.Email);
                
                // Keep the session cookie small: store only what the app needs.
                ctx.Principal = IdentityMapper.ToPrincipal(identity);
                ctx.Properties!.IsPersistent = true;
            },

            OnRemoteFailure = ctx =>
            {
                ctx.Response.Redirect("/Error");
                ctx.HandleResponse();
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Create or update the database schema before serving requests.
await DatabaseInitializer.MigrateAsync(app.Services.GetRequiredService<IDbContextFactory<PacerDbContext>>(), app.Logger);

app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "same-origin";
        h["X-Frame-Options"] = "DENY";
        // Never cache pages; static assets keep their own cache headers.
        if (ctx.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            h["Cache-Control"] = "no-store";
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet("/health-check/pingdom-xml", () => {
    var xmlContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><pingdom_http_custom_check><status>OK</status></pingdom_http_custom_check>";

    return Results.Text(
        xmlContent, 
        "application/xml", 
        System.Text.Encoding.UTF8, 
        (int)HttpStatusCode.OK );
}).AllowAnonymous();

app.MapGet("/login", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = UrlSafety.LocalOrDefault(returnUrl) },
        [OpenIdConnectDefaults.AuthenticationScheme])).AllowAnonymous();

// Ends the Pacer session only. The Authentik session is left alone.
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/signed-out");
}).AllowAnonymous();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
return 0;