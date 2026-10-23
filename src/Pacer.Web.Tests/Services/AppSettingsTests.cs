using Pacer.Web.Services;

namespace Pacer.Web.Tests.Services;

public class AppSettingsTests
{
    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetTempFileName();

        public TempFile(string content) => File.WriteAllText(Path, content);

        public void Dispose() => File.Delete(Path);
    }

    private static Dictionary<string, string> BaseEnv() => new()
    {
        ["OIDC_ISSUER"] = "https://auth.example.com/application/o/pacer/",
        ["OIDC_CLIENT_ID"] = "client-id",
        ["OIDC_CLIENT_SECRET"] = "client-secret",
        ["POSTGRES_HOSTNAME"] = "db",
        ["POSTGRES_PASSWORD"] = "pw",
    };

    private static Func<string, string?> Lookup(Dictionary<string, string> values) =>
        name => values.TryGetValue(name, out var v) ? v : null;

    [Fact]
    public void Minimal_environment_loads_with_defaults()
    {
        var s = AppSettings.Load(Lookup(BaseEnv()));

        Assert.Equal("https://auth.example.com/application/o/pacer/", s.OidcIssuer);
        Assert.Equal("client-id", s.OidcClientId);
        Assert.Equal("client-secret", s.OidcClientSecret);
        Assert.Equal(new[] { "openid", "profile", "email" }, s.OidcScopes);
        Assert.Equal("", s.BaseUrl);
        Assert.True(s.CookieSecure);
        Assert.Equal("UTC", s.Timezone);
        Assert.Equal("db", s.DbHost);
        Assert.Equal(5432, s.DbPort);
        Assert.Equal("pacer", s.DbName);
        Assert.Equal("pacer", s.DbUser);
        Assert.Equal("pw", s.DbPassword);
        Assert.Equal("Prefer", s.DbSslMode);
    }

    [Theory]
    [InlineData("OIDC_ISSUER")]
    [InlineData("OIDC_CLIENT_ID")]
    [InlineData("OIDC_CLIENT_SECRET")]
    [InlineData("POSTGRES_HOSTNAME")]
    [InlineData("POSTGRES_PASSWORD")]
    public void Missing_required_setting_names_the_setting(string name)
    {
        var env = BaseEnv();
        env.Remove(name);

        var ex = Assert.Throws<InvalidOperationException>(() => AppSettings.Load(Lookup(env)));

        Assert.Contains(name, ex.Message);
        Assert.Contains(name + "_FILE", ex.Message);
    }

    [Fact]
    public void Whitespace_only_value_counts_as_missing()
    {
        var env = BaseEnv();
        env["POSTGRES_HOSTNAME"] = "   ";

        Assert.Throws<InvalidOperationException>(() => AppSettings.Load(Lookup(env)));
    }

    [Fact]
    public void Values_are_trimmed()
    {
        var env = BaseEnv();
        env["POSTGRES_HOSTNAME"] = "  db.internal  ";

        Assert.Equal("db.internal", AppSettings.Load(Lookup(env)).DbHost);
    }

    [Fact]
    public void File_setting_is_read_and_trimmed()
    {
        using var file = new TempFile("  s3cret\n");
        var env = BaseEnv();
        env.Remove("POSTGRES_PASSWORD");
        env["POSTGRES_PASSWORD_FILE"] = file.Path;

        Assert.Equal("s3cret", AppSettings.Load(Lookup(env)).DbPassword);
    }

    [Fact]
    public void File_setting_wins_over_the_plain_value()
    {
        using var file = new TempFile("from-file");
        var env = BaseEnv();
        env["POSTGRES_PASSWORD"] = "from-env";
        env["POSTGRES_PASSWORD_FILE"] = file.Path;

        Assert.Equal("from-file", AppSettings.Load(Lookup(env)).DbPassword);
    }

    [Fact]
    public void Every_secret_can_come_from_a_file()
    {
        using var id = new TempFile("file-id");
        using var secret = new TempFile("file-secret");
        var env = BaseEnv();
        env.Remove("OIDC_CLIENT_ID");
        env.Remove("OIDC_CLIENT_SECRET");
        env["OIDC_CLIENT_ID_FILE"] = id.Path;
        env["OIDC_CLIENT_SECRET_FILE"] = secret.Path;

        var s = AppSettings.Load(Lookup(env));

        Assert.Equal("file-id", s.OidcClientId);
        Assert.Equal("file-secret", s.OidcClientSecret);
    }

    [Fact]
    public void Unreadable_file_throws_with_the_setting_name_and_path()
    {
        var env = BaseEnv();
        env["POSTGRES_PASSWORD_FILE"] = "/definitely/not/here/secret";

        var ex = Assert.Throws<InvalidOperationException>(() => AppSettings.Load(Lookup(env)));

        Assert.Contains("POSTGRES_PASSWORD_FILE", ex.Message);
        Assert.Contains("/definitely/not/here/secret", ex.Message);
    }

    [Fact]
    public void Empty_file_variable_is_ignored()
    {
        var env = BaseEnv();
        env["POSTGRES_PASSWORD_FILE"] = "";

        Assert.Equal("pw", AppSettings.Load(Lookup(env)).DbPassword);
    }

    [Fact]
    public void Empty_secret_file_counts_as_missing()
    {
        using var file = new TempFile("\n");
        var env = BaseEnv();
        env.Remove("POSTGRES_PASSWORD");
        env["POSTGRES_PASSWORD_FILE"] = file.Path;

        Assert.Throws<InvalidOperationException>(() => AppSettings.Load(Lookup(env)));
    }

    [Fact]
    public void Scopes_are_split_on_spaces()
    {
        var env = BaseEnv();
        env["OIDC_SCOPES"] = "openid   email  groups";

        Assert.Equal(new[] { "openid", "email", "groups" }, AppSettings.Load(Lookup(env)).OidcScopes);
    }

    [Fact]
    public void Trailing_slashes_are_removed_from_the_base_url()
    {
        var env = BaseEnv();
        env["APP_BASE_URL"] = "https://pacer.example.com//";

        Assert.Equal("https://pacer.example.com", AppSettings.Load(Lookup(env)).BaseUrl);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("", true)]
    public void Cookie_secure_is_only_disabled_by_false(string value, bool expected)
    {
        var env = BaseEnv();
        env["COOKIE_SECURE"] = value;

        Assert.Equal(expected, AppSettings.Load(Lookup(env)).CookieSecure);
    }

    [Fact]
    public void Optional_database_and_timezone_settings_are_honoured()
    {
        var env = BaseEnv();
        env["POSTGRES_PORT"] = "5433";
        env["POSTGRES_DB"] = "goals";
        env["POSTGRES_USER"] = "goal_user";
        env["DB_SSL_MODE"] = "Require";
        env["TZ"] = "Europe/London";

        var s = AppSettings.Load(Lookup(env));

        Assert.Equal(5433, s.DbPort);
        Assert.Equal("goals", s.DbName);
        Assert.Equal("goal_user", s.DbUser);
        Assert.Equal("Require", s.DbSslMode);
        Assert.Equal("Europe/London", s.Timezone);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    public void Invalid_port_is_rejected(string port)
    {
        var env = BaseEnv();
        env["POSTGRES_PORT"] = port;

        var ex = Assert.Throws<InvalidOperationException>(() => AppSettings.Load(Lookup(env)));

        Assert.Contains("POSTGRES_PORT", ex.Message);
    }

    [Fact]
    public void Read_returns_the_fallback_when_nothing_is_set()
    {
        var value = AppSettings.Read(Lookup(new Dictionary<string, string>()), "NOPE", "fallback");

        Assert.Equal("fallback", value);
    }

    [Fact]
    public void Read_returns_null_when_nothing_is_set_and_there_is_no_fallback()
    {
        Assert.Null(AppSettings.Read(Lookup(new Dictionary<string, string>()), "NOPE"));
    }
}
