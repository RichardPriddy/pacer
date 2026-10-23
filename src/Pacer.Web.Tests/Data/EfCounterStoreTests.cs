using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data;
using Pacer.Web.Models;

namespace Pacer.Web.Tests.Data;

/// <summary>Runs the real EF Core queries against an in-memory SQLite database.</summary>
public class EfCounterStoreTests : IDisposable
{
    private const string Alice = "alice";
    private const string Bob = "bob";

    private static readonly DateOnly Date = new(2027, 2, 28);

    private readonly SqliteDatabase _database = new();
    private readonly EfCounterStore _store;

    public EfCounterStoreTests()
    {
        _store = _database.NewStore();
        _store.UpsertUserAsync(Alice, "Alice", "alice@example.com").GetAwaiter().GetResult();
        _store.UpsertUserAsync(Bob, "Bob", null).GetAwaiter().GetResult();
    }

    public void Dispose() => _database.Dispose();

    private static CounterInput Input(string name = "Counter", int target = 10, int current = 0) =>
        new(name, target, Date, current);

    // ---- users ----

    [Fact]
    public async Task Upsert_inserts_a_new_user_then_updates_the_same_row()
    {
        await _store.UpsertUserAsync("carol", "Carol", "carol@example.com");
        await _store.UpsertUserAsync("carol", "Carol Renamed", null);

        await using var db = _database.Factory.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Sub == "carol");
        Assert.Equal("Carol Renamed", user.Name);
        Assert.Null(user.Email);
    }

    [Fact]
    public async Task A_new_user_has_no_default_counter()
    {
        Assert.Null(await _store.GetDefaultCounterIdAsync(Alice));
    }

    [Fact]
    public async Task An_unknown_user_has_no_default_counter()
    {
        Assert.Null(await _store.GetDefaultCounterIdAsync("nobody"));
    }

    // ---- create and read ----

    [Fact]
    public async Task Created_counter_round_trips_including_the_date()
    {
        var id = await _store.CreateCounterAsync(Alice, Input("Pages", 500));

        var counter = await _store.GetCounterAsync(Alice, id);

        Assert.Equal(new Counter(id, "Pages", 500, 0, Date), counter);
    }

    [Fact]
    public async Task Creating_a_counter_for_an_unknown_user_is_rejected_by_the_foreign_key()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _store.CreateCounterAsync("nobody", Input()));
    }

    [Fact]
    public async Task A_failed_create_leaves_nothing_behind()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _store.CreateCounterAsync("nobody", Input()));

        await using var db = _database.Factory.CreateDbContext();
        Assert.Equal(0, await db.Counters.CountAsync());
    }

    [Fact]
    public async Task Ids_are_unique_across_users()
    {
        var one = await _store.CreateCounterAsync(Alice, Input());
        var two = await _store.CreateCounterAsync(Bob, Input());

        Assert.NotEqual(one, two);
    }

    [Fact]
    public async Task First_counter_becomes_the_default_and_the_second_does_not()
    {
        var first = await _store.CreateCounterAsync(Alice, Input("First"));
        await _store.CreateCounterAsync(Alice, Input("Second"));

        Assert.Equal(first, await _store.GetDefaultCounterIdAsync(Alice));
    }

    [Fact]
    public async Task Each_user_gets_their_own_default()
    {
        var alices = await _store.CreateCounterAsync(Alice, Input("Alice's"));
        var bobs = await _store.CreateCounterAsync(Bob, Input("Bob's"));

        Assert.Equal(alices, await _store.GetDefaultCounterIdAsync(Alice));
        Assert.Equal(bobs, await _store.GetDefaultCounterIdAsync(Bob));
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        Assert.Null(await _store.GetCounterAsync(Alice, 999));
    }

    [Fact]
    public async Task List_is_ordered_by_name_ignoring_case()
    {
        await _store.CreateCounterAsync(Alice, Input("banana"));
        await _store.CreateCounterAsync(Alice, Input("Apple"));
        await _store.CreateCounterAsync(Alice, Input("cherry"));

        var names = (await _store.ListCountersAsync(Alice)).Select(c => c.Name).ToArray();

        Assert.Equal(new[] { "Apple", "banana", "cherry" }, names);
    }

    [Fact]
    public async Task List_breaks_name_ties_by_creation_order()
    {
        var first = await _store.CreateCounterAsync(Alice, Input("Same"));
        var second = await _store.CreateCounterAsync(Alice, Input("same"));

        var ids = (await _store.ListCountersAsync(Alice)).Select(c => c.Id).ToArray();

        Assert.Equal(new[] { first, second }, ids);
    }

    [Fact]
    public async Task List_is_empty_for_a_user_without_counters()
    {
        Assert.Empty(await _store.ListCountersAsync(Alice));
    }

    // ---- counting ----

    [Fact]
    public async Task Bump_adds_and_subtracts()
    {
        var id = await _store.CreateCounterAsync(Alice, Input());

        Assert.Equal(1, (await _store.BumpAsync(Alice, id, 1))!.Current);
        Assert.Equal(2, (await _store.BumpAsync(Alice, id, 1))!.Current);
        Assert.Equal(1, (await _store.BumpAsync(Alice, id, -1))!.Current);
    }

    [Fact]
    public async Task Decrement_at_zero_stays_at_zero()
    {
        var id = await _store.CreateCounterAsync(Alice, Input());

        var counter = await _store.BumpAsync(Alice, id, -1);

        Assert.Equal(0, counter!.Current);
    }

    [Fact]
    public async Task A_large_decrease_stops_at_zero()
    {
        var id = await _store.CreateCounterAsync(Alice, Input(current: 0));
        await _store.BumpAsync(Alice, id, 1);

        var counter = await _store.BumpAsync(Alice, id, -10);

        Assert.Equal(0, counter!.Current);
    }

    [Fact]
    public async Task Bump_persists_the_new_count()
    {
        var id = await _store.CreateCounterAsync(Alice, Input());

        await _store.BumpAsync(Alice, id, 1);
        await _store.BumpAsync(Alice, id, 1);

        Assert.Equal(2, (await _store.GetCounterAsync(Alice, id))!.Current);
    }

    [Fact]
    public async Task Bump_of_an_unknown_counter_returns_null()
    {
        Assert.Null(await _store.BumpAsync(Alice, 999, 1));
        Assert.Null(await _store.BumpAsync(Alice, 999, -1));
    }

    // ---- update ----

    [Fact]
    public async Task Update_changes_every_field()
    {
        var id = await _store.CreateCounterAsync(Alice, Input("Old", 10));

        var updated = await _store.UpdateCounterAsync(Alice, id, new CounterInput("New", 99, new DateOnly(2028, 3, 4), 5));

        Assert.True(updated);
        Assert.Equal(new Counter(id, "New", 99, 5, new DateOnly(2028, 3, 4)), await _store.GetCounterAsync(Alice, id));
    }

    [Fact]
    public async Task Update_of_an_unknown_counter_returns_false()
    {
        Assert.False(await _store.UpdateCounterAsync(Alice, 999, Input()));
    }

    // ---- default ----

    [Fact]
    public async Task SetDefault_switches_the_default_for_an_owned_counter()
    {
        await _store.CreateCounterAsync(Alice, Input("First"));
        var second = await _store.CreateCounterAsync(Alice, Input("Second"));

        Assert.True(await _store.SetDefaultAsync(Alice, second));

        Assert.Equal(second, await _store.GetDefaultCounterIdAsync(Alice));
    }

    [Fact]
    public async Task SetDefault_of_an_unknown_counter_fails_and_changes_nothing()
    {
        var first = await _store.CreateCounterAsync(Alice, Input());

        Assert.False(await _store.SetDefaultAsync(Alice, 999));

        Assert.Equal(first, await _store.GetDefaultCounterIdAsync(Alice));
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_removes_the_counter()
    {
        var id = await _store.CreateCounterAsync(Alice, Input());

        Assert.True(await _store.DeleteCounterAsync(Alice, id));

        Assert.Null(await _store.GetCounterAsync(Alice, id));
    }

    [Fact]
    public async Task Deleting_the_default_promotes_the_most_recent_remaining_counter()
    {
        var first = await _store.CreateCounterAsync(Alice, Input("First"));
        await _store.CreateCounterAsync(Alice, Input("Second"));
        var third = await _store.CreateCounterAsync(Alice, Input("Third"));

        Assert.True(await _store.DeleteCounterAsync(Alice, first));

        Assert.Equal(third, await _store.GetDefaultCounterIdAsync(Alice));
    }

    [Fact]
    public async Task Deleting_a_non_default_counter_keeps_the_default()
    {
        var first = await _store.CreateCounterAsync(Alice, Input("First"));
        var second = await _store.CreateCounterAsync(Alice, Input("Second"));

        await _store.DeleteCounterAsync(Alice, second);

        Assert.Equal(first, await _store.GetDefaultCounterIdAsync(Alice));
    }

    [Fact]
    public async Task Deleting_the_last_counter_clears_the_default()
    {
        var id = await _store.CreateCounterAsync(Alice, Input());

        await _store.DeleteCounterAsync(Alice, id);

        Assert.Null(await _store.GetDefaultCounterIdAsync(Alice));
        Assert.Empty(await _store.ListCountersAsync(Alice));
    }

    [Fact]
    public async Task Deleting_one_users_default_does_not_touch_another_users_default()
    {
        var alices = await _store.CreateCounterAsync(Alice, Input("Alice's"));
        var bobs = await _store.CreateCounterAsync(Bob, Input("Bob's"));

        await _store.DeleteCounterAsync(Alice, alices);

        Assert.Null(await _store.GetDefaultCounterIdAsync(Alice));
        Assert.Equal(bobs, await _store.GetDefaultCounterIdAsync(Bob));
    }

    [Fact]
    public async Task Delete_of_an_unknown_counter_returns_false()
    {
        Assert.False(await _store.DeleteCounterAsync(Alice, 999));
    }

    // ---- isolation between users ----

    [Fact]
    public async Task Another_users_counter_is_invisible_and_untouchable()
    {
        var id = await _store.CreateCounterAsync(Alice, Input("Private"));

        Assert.Null(await _store.GetCounterAsync(Bob, id));
        Assert.Empty(await _store.ListCountersAsync(Bob));
        Assert.Null(await _store.BumpAsync(Bob, id, 1));
        Assert.Null(await _store.BumpAsync(Bob, id, -1));
        Assert.False(await _store.UpdateCounterAsync(Bob, id, Input("Hacked")));
        Assert.False(await _store.SetDefaultAsync(Bob, id));
        Assert.False(await _store.DeleteCounterAsync(Bob, id));

        var untouched = await _store.GetCounterAsync(Alice, id);
        Assert.Equal("Private", untouched!.Name);
        Assert.Equal(0, untouched.Current);
        Assert.Null(await _store.GetDefaultCounterIdAsync(Bob));
        Assert.Equal(id, await _store.GetDefaultCounterIdAsync(Alice));
    }
}
