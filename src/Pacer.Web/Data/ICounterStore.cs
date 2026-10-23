using Pacer.Web.Models;

namespace Pacer.Web.Data;

/// <summary>
/// Storage for users and their counters. Every counter operation takes the owner's OIDC subject
/// and must never touch another user's data.
/// </summary>
public interface ICounterStore
{
    Task UpsertUserAsync(string sub, string name, string? email);

    Task<long?> GetDefaultCounterIdAsync(string sub);

    /// <summary>Makes the counter the user's default. Returns false if the user does not own it.</summary>
    Task<bool> SetDefaultAsync(string sub, long counterId);

    /// <summary>The user's counters, ordered by name (case-insensitive).</summary>
    Task<IReadOnlyList<Counter>> ListCountersAsync(string sub);

    Task<Counter?> GetCounterAsync(string sub, long id);

    /// <summary>Creates a counter. The user's first counter automatically becomes their default.</summary>
    Task<long> CreateCounterAsync(string sub, CounterInput input);

    Task<bool> UpdateCounterAsync(string sub, long id, CounterInput input);

    /// <summary>Adds delta to the count (never below zero). Returns null if the user does not own the counter.</summary>
    Task<Counter?> BumpAsync(string sub, long id, int delta);

    /// <summary>Deletes a counter. If it was the default, the most recently created remaining counter takes over.</summary>
    Task<bool> DeleteCounterAsync(string sub, long id);
}
