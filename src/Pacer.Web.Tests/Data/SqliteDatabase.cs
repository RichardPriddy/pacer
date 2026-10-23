using Microsoft.Data.Sqlite;
using Pacer.Web.Data;
using Pacer.Web.Tests.Fakes;

namespace Pacer.Web.Tests.Data;

/// <summary>A fresh in-memory SQLite database with the app's schema, for fast tests of the EF Core code.</summary>
public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory Factory { get; }

    public SqliteDatabase()
    {
        // An in-memory database lives only as long as its connection, so keep one open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // SQLite only enforces foreign keys when asked to.
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        Factory = TestDbContextFactory.ForSqlite(_connection);

        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public EfCounterStore NewStore() => new(Factory);

    public void Dispose() => _connection.Dispose();
}
