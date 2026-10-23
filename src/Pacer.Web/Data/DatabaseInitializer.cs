using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Pacer.Web.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations. Retries while PostgreSQL is still starting. When several replicas start
    /// together, the Npgsql provider takes a database lock so only one of them runs the migrations.
    /// </summary>
    public static async Task MigrateAsync(
        IDbContextFactory<PacerDbContext> factory, ILogger logger, int attempts = 30, TimeSpan? delay = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync();
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < attempts && ex is NpgsqlException or TimeoutException)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Attempts}): {Message}", attempt, attempts, ex.Message);
                await Task.Delay(delay ?? TimeSpan.FromSeconds(2));
            }
        }
    }
}
