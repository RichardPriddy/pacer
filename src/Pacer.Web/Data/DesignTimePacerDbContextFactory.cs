using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pacer.Web.Data;

/// <summary>
/// Lets `dotnet ef migrations add` create the context without starting the app or needing real settings.
/// The connection string is never used to connect when adding migrations.
/// </summary>
public sealed class DesignTimePacerDbContextFactory : IDesignTimeDbContextFactory<PacerDbContext>
{
    public PacerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PacerDbContext>()
            .UseNpgsql("Host=localhost;Database=pacer_design;Username=pacer;Password=design")
            .Options);
}
