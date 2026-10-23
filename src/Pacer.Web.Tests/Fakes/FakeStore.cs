using Pacer.Web.Data;
using Pacer.Web.Models;

namespace Pacer.Web.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ICounterStore"/> that follows the same rules as the PostgreSQL store:
/// every operation is scoped to the owning user, counters need an existing user,
/// the first counter becomes the default, and deleting the default promotes the newest remaining counter.
/// </summary>
public sealed class FakeStore : ICounterStore
{
    private sealed class Row
    {
        public long Id { get; init; }
        public string Sub { get; init; } = "";
        public string Name { get; set; } = "";
        public int Target { get; set; }
        public int Current { get; set; }
        public DateOnly TargetDate { get; set; }
    }

    private readonly Dictionary<string, long?> _users = new();
    private readonly List<Row> _rows = [];
    private long _nextId = 1;

    public List<(string Sub, string Name, string? Email)> Upserts { get; } = [];

    public void AddUser(string sub) => _users.TryAdd(sub, null);

    public Task UpsertUserAsync(string sub, string name, string? email)
    {
        _users.TryAdd(sub, null);
        Upserts.Add((sub, name, email));
        return Task.CompletedTask;
    }

    public Task<long?> GetDefaultCounterIdAsync(string sub) =>
        Task.FromResult(_users.TryGetValue(sub, out var id) ? id : null);

    public Task<bool> SetDefaultAsync(string sub, long counterId)
    {
        var owned = _users.ContainsKey(sub) && _rows.Any(r => r.Id == counterId && r.Sub == sub);
        if (owned) _users[sub] = counterId;
        return Task.FromResult(owned);
    }

    public Task<IReadOnlyList<Counter>> ListCountersAsync(string sub)
    {
        IReadOnlyList<Counter> list = _rows
            .Where(r => r.Sub == sub)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .Select(ToCounter)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<Counter?> GetCounterAsync(string sub, long id) =>
        Task.FromResult(Find(sub, id) is { } row ? ToCounter(row) : null);

    public Task<long> CreateCounterAsync(string sub, CounterInput input)
    {
        if (!_users.ContainsKey(sub))
            throw new InvalidOperationException("Foreign key violation: the user does not exist.");

        var row = new Row
        {
            Id = _nextId++,
            Sub = sub,
            Name = input.Name,
            Target = input.Target,
            TargetDate = input.TargetDate,
        };
        _rows.Add(row);
        _users[sub] ??= row.Id;
        return Task.FromResult(row.Id);
    }

    public Task<bool> UpdateCounterAsync(string sub, long id, CounterInput input)
    {
        var row = Find(sub, id);
        if (row is null) return Task.FromResult(false);

        row.Name = input.Name;
        row.Target = input.Target;
        row.TargetDate = input.TargetDate;
        row.Current = input.Current;
        return Task.FromResult(true);
    }

    public Task<Counter?> BumpAsync(string sub, long id, int delta)
    {
        var row = Find(sub, id);
        if (row is null) return Task.FromResult<Counter?>(null);

        row.Current = Math.Max(row.Current + delta, 0);
        return Task.FromResult<Counter?>(ToCounter(row));
    }

    public Task<bool> DeleteCounterAsync(string sub, long id)
    {
        var row = Find(sub, id);
        if (row is null) return Task.FromResult(false);

        _rows.Remove(row);
        if (_users.TryGetValue(sub, out var defaultId) && defaultId == id)
            _users[sub] = _rows.Where(r => r.Sub == sub).OrderByDescending(r => r.Id).Select(r => (long?)r.Id).FirstOrDefault();
        return Task.FromResult(true);
    }

    private Row? Find(string sub, long id) => _rows.FirstOrDefault(r => r.Id == id && r.Sub == sub);

    private static Counter ToCounter(Row r) => new(r.Id, r.Name, r.Target, r.Current, r.TargetDate);
}
