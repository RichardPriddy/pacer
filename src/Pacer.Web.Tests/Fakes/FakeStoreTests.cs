using Pacer.Web.Models;

namespace Pacer.Web.Tests.Fakes;

/// <summary>Guards the fake itself, so the service tests built on it can be trusted.</summary>
public class FakeStoreTests
{
    private static CounterInput Input(string name = "C", int target = 10) => new(name, target, new DateOnly(2027, 1, 1), 0);

    [Fact]
    public async Task Creating_for_an_unknown_user_throws_like_a_foreign_key()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateCounterAsync("nobody", Input()));
    }

    [Fact]
    public async Task Upsert_registers_the_user_and_records_the_call()
    {
        var store = new FakeStore();

        await store.UpsertUserAsync("u1", "Ann", "ann@example.com");
        var id = await store.CreateCounterAsync("u1", Input());

        Assert.True(id > 0);
        Assert.Equal(("u1", "Ann", "ann@example.com"), Assert.Single(store.Upserts));
    }

    [Fact]
    public async Task Ids_are_unique_across_users()
    {
        var store = new FakeStore();
        store.AddUser("a");
        store.AddUser("b");

        var one = await store.CreateCounterAsync("a", Input());
        var two = await store.CreateCounterAsync("b", Input());

        Assert.NotEqual(one, two);
    }

    [Fact]
    public async Task Bump_clamps_at_zero()
    {
        var store = new FakeStore();
        store.AddUser("a");
        var id = await store.CreateCounterAsync("a", Input());

        var counter = await store.BumpAsync("a", id, -5);

        Assert.Equal(0, counter!.Current);
    }

    [Fact]
    public async Task Operations_on_another_users_counter_do_nothing()
    {
        var store = new FakeStore();
        store.AddUser("a");
        store.AddUser("b");
        var id = await store.CreateCounterAsync("a", Input());

        Assert.Null(await store.GetCounterAsync("b", id));
        Assert.Null(await store.BumpAsync("b", id, 1));
        Assert.False(await store.UpdateCounterAsync("b", id, Input("x")));
        Assert.False(await store.SetDefaultAsync("b", id));
        Assert.False(await store.DeleteCounterAsync("b", id));
        Assert.NotNull(await store.GetCounterAsync("a", id));
    }
}
