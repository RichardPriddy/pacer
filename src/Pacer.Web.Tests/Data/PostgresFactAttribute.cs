namespace Pacer.Web.Tests.Data;

/// <summary>
/// A fact that only runs when PACER_TEST_DB holds an Npgsql connection string for a throw-away database,
/// for example: Host=localhost;Database=pacer_test;Username=pacer;Password=dev
/// Without it the test is reported as skipped.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "PACER_TEST_DB";

    public static string? ConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            Skip = $"Set {EnvironmentVariable} to run the PostgreSQL integration tests.";
    }
}
