using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data;
using Pacer.Web.Data.Entities;
using Pacer.Web.Models;
using Pacer.Web.Tests.Fakes;

namespace Pacer.Web.Tests.Data;

/// <summary>
/// Checks the EF Core model and migrations without needing a database connection.
/// If the migrations test fails, run: dotnet ef migrations add &lt;Name&gt; --project src/Pacer
/// </summary>
public class ModelAndMigrationTests
{
    private static PacerDbContext NewContext() =>
        TestDbContextFactory.ForPostgres("Host=localhost;Database=never_connected;Username=x;Password=x").CreateDbContext();

    [Fact]
    public void Migrations_are_in_sync_with_the_model()
    {
        using var db = NewContext();

        Assert.False(db.Database.HasPendingModelChanges(),
            "The model has changes that are not in a migration. Add a migration with `dotnet ef migrations add`.");
    }

    [Fact]
    public void The_initial_migration_is_present()
    {
        using var db = NewContext();

        Assert.Contains(db.Database.GetMigrations().ToList(), name => name.EndsWith("_Initial_Build"));
    }

    [Fact]
    public void Tables_are_named_as_expected()
    {
        using var db = NewContext();

        Assert.Equal("Users", db.Model.FindEntityType(typeof(UserEntity))!.GetTableName());
        Assert.Equal("Counters", db.Model.FindEntityType(typeof(CounterEntity))!.GetTableName());
        Assert.Equal("DataProtectionKeys", db.Model.FindEntityType(typeof(DataProtectionKeyEntity))!.GetTableName());
    }

    [Fact]
    public void Counter_name_column_matches_the_validators_limit()
    {
        using var db = NewContext();

        var name = db.Model.FindEntityType(typeof(CounterEntity))!.FindProperty(nameof(CounterEntity.Name))!;

        Assert.Equal(CounterValidator.MaxNameLength, name.GetMaxLength());
    }

    [Fact]
    public void Deleting_a_user_cascades_to_their_counters()
    {
        using var db = NewContext();

        var foreignKey = Assert.Single(db.Model.FindEntityType(typeof(CounterEntity))!.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(CounterEntity.UserSub), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void User_subject_is_the_primary_key()
    {
        using var db = NewContext();

        var key = db.Model.FindEntityType(typeof(UserEntity))!.FindPrimaryKey()!;

        Assert.Equal(nameof(UserEntity.Sub), Assert.Single(key.Properties).Name);
    }
}
