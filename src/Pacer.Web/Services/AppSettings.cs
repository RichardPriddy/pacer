namespace Pacer.Web.Services;

public sealed class AppSettings
{
    public required string OidcIssuer { get; init; }
    public required string OidcClientId { get; init; }
    public required string OidcClientSecret { get; init; }
    public required string[] OidcScopes { get; init; }
    public string BaseUrl { get; init; } = "";
    public bool CookieSecure { get; init; } = true;
    public string Timezone { get; init; } = "UTC";

    public required string DbHost { get; init; }
    public int DbPort { get; init; } = 5432;
    public required string DbName { get; init; }
    public required string DbUser { get; init; }
    public required string DbPassword { get; init; }
    public string DbSslMode { get; init; } = "Prefer";

    /// <summary>Reads one setting using the NAME / NAME_FILE convention.</summary>
    public static string? Read(Func<string, string?> env, string name, string? fallback = null)
    {
        var file = env(name + "_FILE");
        if (!string.IsNullOrWhiteSpace(file))
        {
            string content;
            try
            {
                content = File.ReadAllText(file).Trim();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"{name}_FILE points to '{file}', which cannot be read: {ex.Message}", ex);
            }

            // An empty secret file counts as "not set".
            return content.Length == 0 ? fallback : content;
        }

        var value = env(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public static AppSettings Load() => Load(Environment.GetEnvironmentVariable);

    public static AppSettings Load(Func<string, string?> env)
    {
        string? Get(string name, string? fallback = null) => Read(env, name, fallback);

        string Require(string name) =>
            Get(name) ?? throw new InvalidOperationException($"Missing required setting: set {name} or {name}_FILE");

        var portText = Get("POSTGRES_PORT", "5432")!;
        if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException($"POSTGRES_PORT must be a number between 1 and 65535, but was '{portText}'.");

        return new AppSettings
        {
            OidcIssuer = Require("OIDC_ISSUER"),
            OidcClientId = Require("OIDC_CLIENT_ID"),
            OidcClientSecret = Require("OIDC_CLIENT_SECRET"),
            OidcScopes = (Get("OIDC_SCOPES", "openid profile email") ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            BaseUrl = (Get("APP_BASE_URL", "") ?? "").TrimEnd('/'),
            CookieSecure = !string.Equals(Get("COOKIE_SECURE", "true"), "false", StringComparison.OrdinalIgnoreCase),
            Timezone = Get("TZ", "UTC") ?? "UTC",

            DbHost = Require("POSTGRES_HOSTNAME"),
            DbPort = port,
            DbName = Get("POSTGRES_DB", "pacer")!,
            DbUser = Get("POSTGRES_USER", "pacer")!,
            DbPassword = Require("POSTGRES_PASSWORD"),
            DbSslMode = Get("DB_SSL_MODE", "Prefer")!,
        };
    }
}