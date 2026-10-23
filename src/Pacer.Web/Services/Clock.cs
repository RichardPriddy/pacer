namespace Pacer.Web.Services;

public interface IClock
{
    DateOnly Today();
}

/// <summary>Decides what "today" is, in the configured timezone.</summary>
public sealed class Clock : IClock
{
    private readonly Func<DateTime> _utcNow;

    public TimeZoneInfo TimeZone { get; }

    public Clock(string timezoneId, Func<DateTime>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        try
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            Console.Error.WriteLine($"Unknown APP_TIMEZONE '{timezoneId}', falling back to UTC.");
            TimeZone = TimeZoneInfo.Utc;
        }
    }

    public DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc), TimeZone));
}
