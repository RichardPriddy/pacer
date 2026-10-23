using Pacer.Web.Data;
using Pacer.Web.Models;

namespace Pacer.Web.Services;

public sealed record SaveResult(long? Id, IReadOnlyList<string> Errors, bool NotFound = false)
{
    public bool Ok => !NotFound && Errors.Count == 0;
}

/// <summary>Everything the UI does with counters. Every call is scoped to the user's OIDC subject.</summary>
public sealed class CounterService(ICounterStore store, IClock clock)
{
    public DateOnly Today() => clock.Today();

    public async Task<CounterView?> GetDefaultAsync(string sub)
    {
        var defaultId = await store.GetDefaultCounterIdAsync(sub);
        if (defaultId is null) return null;

        var counter = await store.GetCounterAsync(sub, defaultId.Value);
        return counter is null ? null : ToView(counter, true);
    }

    public async Task<bool> HasCountersAsync(string sub) =>
        (await store.ListCountersAsync(sub)).Count > 0;

    public async Task<CounterView?> GetAsync(string sub, long id)
    {
        var counter = await store.GetCounterAsync(sub, id);
        if (counter is null) return null;

        var defaultId = await store.GetDefaultCounterIdAsync(sub);
        return ToView(counter, defaultId == id);
    }

    public async Task<IReadOnlyList<CounterView>> ListAsync(string sub)
    {
        var defaultId = await store.GetDefaultCounterIdAsync(sub);
        var counters = await store.ListCountersAsync(sub);
        return counters.Select(c => ToView(c, c.Id == defaultId)).ToList();
    }

    public async Task<SaveResult> CreateAsync(string sub, CounterFormValues form)
    {
        var (input, errors) = CounterValidator.Parse(
            form.Name, form.Target, form.TargetDate, null, creating: true, clock.Today());
        if (input is null) return new SaveResult(null, errors);

        var id = await store.CreateCounterAsync(sub, input);
        return new SaveResult(id, []);
    }

    public async Task<SaveResult> UpdateAsync(string sub, long id, CounterFormValues form)
    {
        var (input, errors) = CounterValidator.Parse(
            form.Name, form.Target, form.TargetDate, form.Current, creating: false, clock.Today());
        if (input is null) return new SaveResult(id, errors);

        return await store.UpdateCounterAsync(sub, id, input)
            ? new SaveResult(id, [])
            : new SaveResult(id, [], NotFound: true);
    }

    public Task<CounterView?> IncrementAsync(string sub, long id) => BumpAsync(sub, id, 1);

    public Task<CounterView?> DecrementAsync(string sub, long id) => BumpAsync(sub, id, -1);

    public Task<bool> SetDefaultAsync(string sub, long id) => store.SetDefaultAsync(sub, id);

    public Task<bool> DeleteAsync(string sub, long id) => store.DeleteCounterAsync(sub, id);

    private async Task<CounterView?> BumpAsync(string sub, long id, int delta)
    {
        var counter = await store.BumpAsync(sub, id, delta);
        if (counter is null) return null;

        var defaultId = await store.GetDefaultCounterIdAsync(sub);
        return ToView(counter, defaultId == id);
    }

    private CounterView ToView(Counter counter, bool isDefault) =>
        new(counter, Pacing.Compute(counter, clock.Today()), isDefault);
}
