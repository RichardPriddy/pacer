using Npgsql;
using Pacer.Web.Data;
using Pacer.Web.Services;

namespace Pacer.Web.Tests.Data;

public class ConnectionStringTests
{
    private static AppSettings Settings(string? sslMode = null, string? port = null)
    {
        var env = new Dictionary<string, string>
        {
            ["OIDC_ISSUER"] = "https://auth.example.com/application/o/pacer/",
            ["OIDC_CLIENT_ID"] = "id",
            ["OIDC_CLIENT_SECRET"] = "secret",
            ["POSTGRES_HOSTNAME"] = "db.internal",
            ["POSTGRES_DB"] = "goals",
            ["POSTGRES_USER"] = "goal_user",
            ["POSTGRES_PASSWORD"] = "p@ss;word=1",
        };
        if (sslMode is not null) env["DB_SSL_MODE"] = sslMode;
        if (port is not null) env["POSTGRES_PORT"] = port;
        return AppSettings.Load(name => env.TryGetValue(name, out var v) ? v : null);
    }

    [Fact]
    public void Settings_are_carried_into_the_connection_string()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStrings.Postgres(Settings(port: "5433")));

        Assert.Equal("db.internal", b.Host);
        Assert.Equal(5433, b.Port);
        Assert.Equal("goals", b.Database);
        Assert.Equal("goal_user", b.Username);
    }

    [Fact]
    public void A_password_with_special_characters_survives()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStrings.Postgres(Settings()));

        Assert.Equal("p@ss;word=1", b.Password);
    }

    [Fact]
    public void Ssl_mode_defaults_to_prefer()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStrings.Postgres(Settings()));

        Assert.Equal(SslMode.Prefer, b.SslMode);
    }

    [Theory]
    [InlineData("Require", SslMode.Require)]
    [InlineData("require", SslMode.Require)]
    [InlineData("Disable", SslMode.Disable)]
    [InlineData("VerifyFull", SslMode.VerifyFull)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    [InlineData("verify_ca", SslMode.VerifyCA)]
    public void Ssl_mode_accepts_common_spellings(string value, SslMode expected)
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStrings.Postgres(Settings(sslMode: value)));

        Assert.Equal(expected, b.SslMode);
    }

    [Fact]
    public void An_unknown_ssl_mode_is_rejected_with_a_helpful_message()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ConnectionStrings.Postgres(Settings(sslMode: "sometimes")));

        Assert.Contains("DB_SSL_MODE", ex.Message);
        Assert.Contains("sometimes", ex.Message);
    }
}
