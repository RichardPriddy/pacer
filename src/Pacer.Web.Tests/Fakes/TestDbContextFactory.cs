using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data;

namespace Pacer.Web.Tests.Fakes;

public sealed class TestDbContextFactory(DbContextOptions<PacerDbContext> options) : IDbContextFactory<PacerDbContext>
{
    public PacerDbContext CreateDbContext() => new(options);

    public static TestDbContextFactory ForSqlite(SqliteConnection openConnection) =>
        new(new DbContextOptionsBuilder<PacerDbContext>().UseSqlite(openConnection).Options);

    public static TestDbContextFactory ForPostgres(string connectionString) =>
        new(new DbContextOptionsBuilder<PacerDbContext>().UseNpgsql(connectionString).Options);
}
