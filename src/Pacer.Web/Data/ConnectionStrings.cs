using Npgsql;
using Pacer.Web.Services;

namespace Pacer.Web.Data;

public static class ConnectionStrings
{
    /// <summary>Builds the Npgsql connection string from the app settings.</summary>
    public static string Postgres(AppSettings s)
    {
        if (!Enum.TryParse<SslMode>(s.DbSslMode.Replace("-", "").Replace("_", ""), ignoreCase: true, out var sslMode))
            throw new InvalidOperationException(
                $"DB_SSL_MODE must be one of Disable, Prefer, Require, VerifyCA or VerifyFull, but was '{s.DbSslMode}'.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = s.DbHost,
            Port = s.DbPort,
            Database = s.DbName,
            Username = s.DbUser,
            Password = s.DbPassword,
            SslMode = sslMode,
        }.ConnectionString;
    }
}

