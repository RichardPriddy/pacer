using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data;
using Pacer.Web.Models;
using Pacer.Web.Tests.Fakes;

namespace Pacer.Web.Tests.Data;

/// <summary>
/// Tests the things SQLite cannot: the real migrations, the Npgsql provider's SQL and PostgreSQL constraints.
/// They need a PostgreSQL database (see <see cref="PostgresFactAttribute"/>) and are skipped without one.
/// Each test uses a brand-new user, so tests never interfere with each other.
/// </summary>
public class PostgresIntegrationTests
{
    private static readonly DateOnly Date = new(2027, 2, 28);

    private static TestDbContextFactory NewFactory() => TestDbContextFactory.ForPostgres(PostgresFactAttribute.ConnectionString!);

    private static CounterInput Input(string name = "Counter", int target = 10) => new(name, target, Date, 0);

    private static async Task<(EfCounterStore Store, string Sub)> NewUserAsync()
    {
        var factory = NewFactory();
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        var store = new EfCounterStore(factory);
        var sub = "test-" + Guid.NewGuid().ToString("N");
        await store.UpsertUserAsync(sub, "Test User", "test@example.com");
        return (store, sub);
    }

    [PostgresFact]
    public async Task Migrations_apply_and_leave_nothing_pending()
    {
        await using var db = NewFactory().CreateDbContext();

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [PostgresFact]
    public async Task Migrating_repeatedly_is_harmless()
    {
        await using var db = NewFactory().CreateDbContext();

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
    }

    [PostgresFact]
    public async Task Several_replicas_migrating_at_once_do_not_collide()
    {
        var factory = NewFactory();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            DatabaseInitializer.MigrateAsync(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)));
    }

    [PostgresFact]
    public async Task Created_counter_round_trips_including_the_date()
    {
        var (store, sub) = await NewUserAsync();

        var id = await store.CreateCounterAsync(sub, Input("Pages", 500));

        Assert.Equal(new Counter(id, "Pages", 500, 0, Date), await store.GetCounterAsync(sub, id));
    }

    [PostgresFact]
    public async Task List_is_ordered_by_name_ignoring_case()
    {
        var (store, sub) = await NewUserAsync();
        await store.CreateCounterAsync(sub, Input("banana"));
        await store.CreateCounterAsync(sub, Input("Apple"));
        await store.CreateCounterAsync(sub, Input("cherry"));

        var names = (await store.ListCountersAsync(sub)).Select(c => c.Name).ToArray();

        Assert.Equal(new[] { "Apple", "banana", "cherry" }, names);
    }

    [PostgresFact]
    public async Task Concurrent_increments_are_not_lost()
    {
        var (store, sub) = await NewUserAsync();
        var id = await store.CreateCounterAsync(sub, Input(target: 1000));

        await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => store.BumpAsync(sub, id, 1)));

        Assert.Equal(25, (await store.GetCounterAsync(sub, id))!.Current);
    }

    [PostgresFact]
    public async Task Decrement_stops_at_zero()
    {
        var (store, sub) = await NewUserAsync();
        var id = await store.CreateCounterAsync(sub, Input());
        await store.BumpAsync(sub, id, 1);

        Assert.Equal(0, (await store.BumpAsync(sub, id, -1))!.Current);
        Assert.Equal(0, (await store.BumpAsync(sub, id, -1))!.Current);
        Assert.Equal(0, (await store.BumpAsync(sub, id, -10))!.Current);
    }

    [PostgresFact]
    public async Task Deleting_the_default_promotes_the_most_recent_remaining_counter()
    {
        var (store, sub) = await NewUserAsync();
        var first = await store.CreateCounterAsync(sub, Input("First"));
        await store.CreateCounterAsync(sub, Input("Second"));
        var third = await store.CreateCounterAsync(sub, Input("Third"));

        Assert.True(await store.DeleteCounterAsync(sub, first));

        Assert.Equal(third, await store.GetDefaultCounterIdAsync(sub));
    }

    [PostgresFact]
    public async Task Counters_are_invisible_and_untouchable_to_other_users()
    {
        var (store, alice) = await NewUserAsync();
        var (_, bob) = await NewUserAsync();
        var id = await store.CreateCounterAsync(alice, Input("Private"));

        Assert.Null(await store.GetCounterAsync(bob, id));
        Assert.Empty(await store.ListCountersAsync(bob));
        Assert.Null(await store.BumpAsync(bob, id, 1));
        Assert.False(await store.UpdateCounterAsync(bob, id, Input("Hacked")));
        Assert.False(await store.SetDefaultAsync(bob, id));
        Assert.False(await store.DeleteCounterAsync(bob, id));
        Assert.Equal("Private", (await store.GetCounterAsync(alice, id))!.Name);
    }

    [PostgresFact]
    public async Task Counter_names_longer_than_the_column_are_rejected()
    {
        var (store, sub) = await NewUserAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => store.CreateCounterAsync(sub, Input(new string('x', 81))));
    }

    [PostgresFact]
    public async Task Removing_a_user_removes_their_counters()
    {
        var (store, sub) = await NewUserAsync();
        await store.CreateCounterAsync(sub, Input());
        await store.CreateCounterAsync(sub, Input("Two"));
        var factory = NewFactory();

        await using (var db = factory.CreateDbContext())
        {
            await db.Users.Where(u => u.Sub == sub).ExecuteDeleteAsync();
        }

        Assert.Empty(await store.ListCountersAsync(sub));
    }

    [PostgresFact]
    public async Task Data_protection_keys_round_trip_through_the_repository()
    {
        await NewUserAsync(); // makes sure the schema exists
        var repository = new EfXmlRepository(NewFactory());
        var keyId = Guid.NewGuid().ToString();

        repository.StoreElement(new XElement("key", new XAttribute("id", keyId)), "test-key");

        Assert.Contains(repository.GetAllElements(), e => (string?)e.Attribute("id") == keyId);
    }
}
