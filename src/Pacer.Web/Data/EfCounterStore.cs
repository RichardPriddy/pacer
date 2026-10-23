using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data.Entities;
using Pacer.Web.Models;

namespace Pacer.Web.Data;

/// <summary>
/// Entity Framework Core implementation of <see cref="ICounterStore"/>.
/// Each call uses its own short-lived context (safe for Blazor Server), and every counter query
/// is filtered by the owner's subject.
/// </summary>
public sealed class EfCounterStore(IDbContextFactory<PacerDbContext> factory) : ICounterStore
{
    public async Task UpsertUserAsync(string sub, string name, string? email)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync();

            var user = await db.Users.FindAsync(sub);
            if (user is null)
            {
                db.Users.Add(new UserEntity { Sub = sub, Name = name, Email = email });
            }
            else
            {
                user.Name = name;
                user.Email = email;
            }

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                // Two first sign-ins at once: the other one inserted the row, so retry as an update.
            }
        }
    }

    public async Task<long?> GetDefaultCounterIdAsync(string sub)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users
            .AsNoTracking()
            .Where(u => u.Sub == sub)
            .Select(u => u.DefaultCounterId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SetDefaultAsync(string sub, long counterId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var owned = await db.Counters.AnyAsync(c => c.Id == counterId && c.UserSub == sub);
        if (!owned) return false;

        var updated = await db.Users
            .Where(u => u.Sub == sub)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DefaultCounterId, (long?)counterId));
        return updated > 0;
    }

    public async Task<IReadOnlyList<Counter>> ListCountersAsync(string sub)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Counters
            .AsNoTracking()
            .Where(c => c.UserSub == sub)
            .OrderBy(c => c.Name.ToLower())
            .ThenBy(c => c.Id)
            .Select(c => new Counter(c.Id, c.Name, c.Target, c.CurrentCount, c.TargetDate))
            .ToListAsync();
    }

    public async Task<Counter?> GetCounterAsync(string sub, long id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await FindAsync(db, sub, id);
    }

    public async Task<long> CreateCounterAsync(string sub, CounterInput input)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var entity = new CounterEntity
        {
            UserSub = sub,
            Name = input.Name,
            Target = input.Target,
            TargetDate = input.TargetDate,
        };
        db.Counters.Add(entity);
        await db.SaveChangesAsync();

        // The user's first counter becomes their default.
        var newId = entity.Id;
        await db.Users
            .Where(u => u.Sub == sub && u.DefaultCounterId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DefaultCounterId, (long?)newId));

        await tx.CommitAsync();
        return newId;
    }

    public async Task<bool> UpdateCounterAsync(string sub, long id, CounterInput input)
    {
        await using var db = await factory.CreateDbContextAsync();
        var updated = await db.Counters
            .Where(c => c.Id == id && c.UserSub == sub)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Name, input.Name)
                .SetProperty(c => c.Target, input.Target)
                .SetProperty(c => c.TargetDate, input.TargetDate)
                .SetProperty(c => c.CurrentCount, input.Current));
        return updated > 0;
    }

    public async Task<Counter?> BumpAsync(string sub, long id, int delta)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Each step is a single atomic UPDATE, so concurrent clicks are never lost.
        await db.Counters
            .Where(c => c.Id == id && c.UserSub == sub && c.CurrentCount + delta >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CurrentCount, c => c.CurrentCount + delta));

        // A decrease that would go below zero stops at zero instead.
        if (delta < 0)
        {
            await db.Counters
                .Where(c => c.Id == id && c.UserSub == sub && c.CurrentCount + delta < 0)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CurrentCount, 0));
        }

        return await FindAsync(db, sub, id);
    }

    public async Task<bool> DeleteCounterAsync(string sub, long id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var deleted = await db.Counters
            .Where(c => c.Id == id && c.UserSub == sub)
            .ExecuteDeleteAsync() > 0;

        if (deleted)
        {
            // If it was the default, the most recently created remaining counter takes over (or none).
            var next = await db.Counters
                .Where(c => c.UserSub == sub)
                .OrderByDescending(c => c.Id)
                .Select(c => (long?)c.Id)
                .FirstOrDefaultAsync();

            await db.Users
                .Where(u => u.Sub == sub && u.DefaultCounterId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DefaultCounterId, next));
        }

        await tx.CommitAsync();
        return deleted;
    }

    private static Task<Counter?> FindAsync(PacerDbContext db, string sub, long id) =>
        db.Counters
            .AsNoTracking()
            .Where(c => c.Id == id && c.UserSub == sub)
            .Select(c => new Counter(c.Id, c.Name, c.Target, c.CurrentCount, c.TargetDate))
            .FirstOrDefaultAsync();
}
