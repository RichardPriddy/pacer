using Pacer.Web.Models;
using Pacer.Web.Services;
using Pacer.Web.Tests.Fakes;

namespace Pacer.Web.Tests.Services;

public class CounterServiceTests
{
    private const string Alice = "alice";
    private const string Bob = "bob";

    private readonly FakeStore _store = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 20));
    private readonly CounterService _svc;

    public CounterServiceTests()
    {
        _svc = new CounterService(_store, _clock);
        _store.AddUser(Alice);
        _store.AddUser(Bob);
    }

    private static CounterFormValues Form(string? name = "Pages", string? target = "100", string? date = "2026-12-31", string? current = null) =>
        new() { Name = name, Target = target, TargetDate = date, Current = current };

    private async Task<long> Create(string sub, string name = "Pages", string target = "100", string date = "2026-12-31")
    {
        var result = await _svc.CreateAsync(sub, Form(name, target, date));
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        return result.Id!.Value;
    }

    // ---- create ----

    [Fact]
    public async Task Create_stores_the_counter_with_a_zero_count()
    {
        var id = await Create(Alice, "Pages", "100", "2026-12-31");

        var view = await _svc.GetAsync(Alice, id);

        Assert.NotNull(view);
        Assert.Equal("Pages", view!.C.Name);
        Assert.Equal(100, view.C.Target);
        Assert.Equal(0, view.C.Current);
        Assert.Equal(new DateOnly(2026, 12, 31), view.C.TargetDate);
    }

    [Fact]
    public async Task Create_trims_the_name()
    {
        var id = await Create(Alice, "  Pages  ");

        Assert.Equal("Pages", (await _svc.GetAsync(Alice, id))!.C.Name);
    }

    [Fact]
    public async Task First_counter_becomes_the_default()
    {
        var id = await Create(Alice);

        var def = await _svc.GetDefaultAsync(Alice);

        Assert.Equal(id, def!.C.Id);
        Assert.True(def.IsDefault);
    }

    [Fact]
    public async Task Second_counter_does_not_replace_the_default()
    {
        var first = await Create(Alice, "First");
        await Create(Alice, "Second");

        Assert.Equal(first, (await _svc.GetDefaultAsync(Alice))!.C.Id);
    }

    [Fact]
    public async Task Invalid_input_returns_errors_and_stores_nothing()
    {
        var result = await _svc.CreateAsync(Alice, Form(name: "", target: "0"));

        Assert.False(result.Ok);
        Assert.Null(result.Id);
        Assert.Equal(2, result.Errors.Count);
        Assert.False(await _svc.HasCountersAsync(Alice));
    }

    [Fact]
    public async Task Create_rejects_a_date_in_the_past()
    {
        var result = await _svc.CreateAsync(Alice, Form(date: "2026-10-19"));

        Assert.False(result.Ok);
        Assert.Contains("today or later", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Create_allows_todays_date()
    {
        var result = await _svc.CreateAsync(Alice, Form(date: "2026-10-20"));

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task Create_uses_the_clock_to_decide_whether_a_date_is_in_the_past()
    {
        _clock.Date = new DateOnly(2027, 1, 1);

        var result = await _svc.CreateAsync(Alice, Form(date: "2026-12-31"));

        Assert.False(result.Ok);
    }

    // ---- read ----

    [Fact]
    public async Task No_default_when_the_user_has_no_counters()
    {
        Assert.Null(await _svc.GetDefaultAsync(Alice));
        Assert.False(await _svc.HasCountersAsync(Alice));
    }

    [Fact]
    public async Task HasCounters_is_true_once_one_exists()
    {
        await Create(Alice);

        Assert.True(await _svc.HasCountersAsync(Alice));
        Assert.False(await _svc.HasCountersAsync(Bob));
    }

    [Fact]
    public async Task Default_view_includes_the_weekly_pace()
    {
        // 28 days to go for 100 clicks: 25 a week
        await Create(Alice, target: "100", date: "2026-11-17");

        var def = await _svc.GetDefaultAsync(Alice);

        Assert.Equal(PaceStatus.Active, def!.S.Status);
        Assert.Equal(25.0, def.S.PerWeek!.Value, 6);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        Assert.Null(await _svc.GetAsync(Alice, 999));
    }

    [Fact]
    public async Task Get_flags_which_counter_is_the_default()
    {
        var first = await Create(Alice, "First");
        var second = await Create(Alice, "Second");

        Assert.True((await _svc.GetAsync(Alice, first))!.IsDefault);
        Assert.False((await _svc.GetAsync(Alice, second))!.IsDefault);
    }

    [Fact]
    public async Task List_is_sorted_by_name_ignoring_case()
    {
        await Create(Alice, "banana");
        await Create(Alice, "Apple");
        await Create(Alice, "cherry");

        var names = (await _svc.ListAsync(Alice)).Select(v => v.C.Name).ToArray();

        Assert.Equal(new[] { "Apple", "banana", "cherry" }, names);
    }

    [Fact]
    public async Task List_flags_the_default()
    {
        await Create(Alice, "A");
        await Create(Alice, "B");

        var flags = (await _svc.ListAsync(Alice)).Select(v => v.IsDefault).ToArray();

        Assert.Equal(new[] { true, false }, flags);
    }

    [Fact]
    public async Task List_is_empty_for_a_new_user()
    {
        Assert.Empty(await _svc.ListAsync(Alice));
    }

    [Fact]
    public async Task Pace_follows_the_clock()
    {
        var id = await Create(Alice, target: "10", date: "2026-10-25");

        _clock.Date = new DateOnly(2026, 10, 25);
        Assert.Equal(PaceStatus.DueToday, (await _svc.GetAsync(Alice, id))!.S.Status);

        _clock.Date = new DateOnly(2026, 10, 26);
        Assert.Equal(PaceStatus.Overdue, (await _svc.GetAsync(Alice, id))!.S.Status);
    }

    // ---- counting ----

    [Fact]
    public async Task Increment_adds_one_and_returns_the_updated_view()
    {
        var id = await Create(Alice);

        var view = await _svc.IncrementAsync(Alice, id);

        Assert.Equal(1, view!.C.Current);
        Assert.Equal(99, view.S.Remaining);
        Assert.True(view.IsDefault);
    }

    [Fact]
    public async Task Increments_accumulate()
    {
        var id = await Create(Alice);

        await _svc.IncrementAsync(Alice, id);
        await _svc.IncrementAsync(Alice, id);
        var view = await _svc.IncrementAsync(Alice, id);

        Assert.Equal(3, view!.C.Current);
    }

    [Fact]
    public async Task Increment_reports_when_the_counter_is_not_the_default()
    {
        await Create(Alice, "First");
        var second = await Create(Alice, "Second");

        Assert.False((await _svc.IncrementAsync(Alice, second))!.IsDefault);
    }

    [Fact]
    public async Task Decrement_subtracts_one()
    {
        var id = await Create(Alice);
        await _svc.IncrementAsync(Alice, id);
        await _svc.IncrementAsync(Alice, id);

        var view = await _svc.DecrementAsync(Alice, id);

        Assert.Equal(1, view!.C.Current);
    }

    [Fact]
    public async Task Decrement_never_goes_below_zero()
    {
        var id = await Create(Alice);

        var view = await _svc.DecrementAsync(Alice, id);

        Assert.Equal(0, view!.C.Current);
    }

    [Fact]
    public async Task Reaching_the_target_completes_the_counter()
    {
        var id = await Create(Alice, target: "2");

        await _svc.IncrementAsync(Alice, id);
        var view = await _svc.IncrementAsync(Alice, id);

        Assert.Equal(PaceStatus.Complete, view!.S.Status);
        Assert.Equal(0, view.S.Remaining);
        Assert.Equal(100.0, view.S.Percent, 6);
    }

    [Fact]
    public async Task Counting_continues_past_the_target()
    {
        var id = await Create(Alice, target: "2");

        for (var i = 0; i < 3; i++) await _svc.IncrementAsync(Alice, id);
        var view = await _svc.GetAsync(Alice, id);

        Assert.Equal(3, view!.C.Current);
        Assert.Equal(PaceStatus.Complete, view.S.Status);
        Assert.Equal(100.0, view.S.Percent, 6);
    }

    [Fact]
    public async Task Increment_of_an_unknown_counter_returns_null()
    {
        Assert.Null(await _svc.IncrementAsync(Alice, 999));
        Assert.Null(await _svc.DecrementAsync(Alice, 999));
    }

    // ---- default ----

    [Fact]
    public async Task SetDefault_changes_the_default()
    {
        await Create(Alice, "First");
        var second = await Create(Alice, "Second");

        Assert.True(await _svc.SetDefaultAsync(Alice, second));

        Assert.Equal(second, (await _svc.GetDefaultAsync(Alice))!.C.Id);
    }

    [Fact]
    public async Task SetDefault_of_an_unknown_counter_fails_and_changes_nothing()
    {
        var first = await Create(Alice);

        Assert.False(await _svc.SetDefaultAsync(Alice, 999));

        Assert.Equal(first, (await _svc.GetDefaultAsync(Alice))!.C.Id);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_removes_the_counter()
    {
        var id = await Create(Alice);

        Assert.True(await _svc.DeleteAsync(Alice, id));

        Assert.Null(await _svc.GetAsync(Alice, id));
    }

    [Fact]
    public async Task Deleting_the_default_promotes_the_most_recent_remaining_counter()
    {
        var first = await Create(Alice, "First");
        await Create(Alice, "Second");
        var third = await Create(Alice, "Third");

        await _svc.DeleteAsync(Alice, first);

        Assert.Equal(third, (await _svc.GetDefaultAsync(Alice))!.C.Id);
    }

    [Fact]
    public async Task Deleting_a_non_default_counter_keeps_the_default()
    {
        var first = await Create(Alice, "First");
        var second = await Create(Alice, "Second");

        await _svc.DeleteAsync(Alice, second);

        Assert.Equal(first, (await _svc.GetDefaultAsync(Alice))!.C.Id);
    }

    [Fact]
    public async Task Deleting_the_last_counter_leaves_no_default()
    {
        var id = await Create(Alice);

        await _svc.DeleteAsync(Alice, id);

        Assert.Null(await _svc.GetDefaultAsync(Alice));
        Assert.False(await _svc.HasCountersAsync(Alice));
    }

    [Fact]
    public async Task Delete_of_an_unknown_counter_returns_false()
    {
        Assert.False(await _svc.DeleteAsync(Alice, 999));
    }

    // ---- update ----

    [Fact]
    public async Task Update_changes_every_field()
    {
        var id = await Create(Alice);

        var result = await _svc.UpdateAsync(Alice, id, Form("Renamed", "50", "2027-01-01", "7"));

        Assert.True(result.Ok);
        var view = await _svc.GetAsync(Alice, id);
        Assert.Equal("Renamed", view!.C.Name);
        Assert.Equal(50, view.C.Target);
        Assert.Equal(new DateOnly(2027, 1, 1), view.C.TargetDate);
        Assert.Equal(7, view.C.Current);
    }

    [Fact]
    public async Task Update_with_invalid_input_returns_errors_and_changes_nothing()
    {
        var id = await Create(Alice, "Original");

        var result = await _svc.UpdateAsync(Alice, id, Form("", "50", "2027-01-01", "7"));

        Assert.False(result.Ok);
        Assert.False(result.NotFound);
        Assert.Single(result.Errors);
        Assert.Equal("Original", (await _svc.GetAsync(Alice, id))!.C.Name);
    }

    [Fact]
    public async Task Update_requires_a_valid_current_count()
    {
        var id = await Create(Alice);

        var result = await _svc.UpdateAsync(Alice, id, Form(current: "abc"));

        Assert.False(result.Ok);
        Assert.Contains("Current count", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Update_allows_a_date_in_the_past()
    {
        var id = await Create(Alice);

        var result = await _svc.UpdateAsync(Alice, id, Form(date: "2020-01-01", current: "0"));

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task Update_of_an_unknown_counter_reports_not_found()
    {
        var result = await _svc.UpdateAsync(Alice, 999, Form(current: "0"));

        Assert.True(result.NotFound);
        Assert.False(result.Ok);
    }

    // ---- isolation between users ----

    [Fact]
    public async Task Users_cannot_see_each_others_counters()
    {
        var id = await Create(Alice, "Private");

        Assert.Null(await _svc.GetAsync(Bob, id));
        Assert.Empty(await _svc.ListAsync(Bob));
        Assert.Null(await _svc.GetDefaultAsync(Bob));
        Assert.False(await _svc.HasCountersAsync(Bob));
    }

    [Fact]
    public async Task Users_cannot_count_on_each_others_counters()
    {
        var id = await Create(Alice);

        Assert.Null(await _svc.IncrementAsync(Bob, id));
        Assert.Null(await _svc.DecrementAsync(Bob, id));

        Assert.Equal(0, (await _svc.GetAsync(Alice, id))!.C.Current);
    }

    [Fact]
    public async Task Users_cannot_delete_each_others_counters()
    {
        var id = await Create(Alice);

        Assert.False(await _svc.DeleteAsync(Bob, id));

        Assert.NotNull(await _svc.GetAsync(Alice, id));
    }

    [Fact]
    public async Task Users_cannot_update_each_others_counters()
    {
        var id = await Create(Alice, "Mine");

        var result = await _svc.UpdateAsync(Bob, id, Form("Hacked", current: "0"));

        Assert.True(result.NotFound);
        Assert.Equal("Mine", (await _svc.GetAsync(Alice, id))!.C.Name);
    }

    [Fact]
    public async Task Users_cannot_make_someone_elses_counter_their_default()
    {
        var alices = await Create(Alice);
        var bobs = await Create(Bob, "Bob's");

        Assert.False(await _svc.SetDefaultAsync(Bob, alices));

        Assert.Equal(bobs, (await _svc.GetDefaultAsync(Bob))!.C.Id);
        Assert.Equal(alices, (await _svc.GetDefaultAsync(Alice))!.C.Id);
    }

    [Fact]
    public async Task Each_user_gets_their_own_default()
    {
        var alices = await Create(Alice, "Alice's");
        var bobs = await Create(Bob, "Bob's");

        Assert.Equal(alices, (await _svc.GetDefaultAsync(Alice))!.C.Id);
        Assert.Equal(bobs, (await _svc.GetDefaultAsync(Bob))!.C.Id);
    }

    // ---- misc ----

    [Fact]
    public void Today_comes_from_the_clock()
    {
        Assert.Equal(new DateOnly(2026, 10, 20), _svc.Today());

        _clock.Date = new DateOnly(2030, 5, 6);

        Assert.Equal(new DateOnly(2030, 5, 6), _svc.Today());
    }

    [Fact]
    public void SaveResult_is_ok_only_without_errors_or_not_found()
    {
        Assert.True(new SaveResult(1, []).Ok);
        Assert.False(new SaveResult(1, ["bad"]).Ok);
        Assert.False(new SaveResult(1, [], NotFound: true).Ok);
    }
}
