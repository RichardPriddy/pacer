using Pacer.Web.Services;

namespace Pacer.Web.Tests.Services;

public class ClockTests
{
    private static Clock At(string timezone, int year, int month, int day, int hour, int minute) =>
        new(timezone, () => new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc));

    [Fact]
    public void Utc_uses_the_utc_date()
    {
        Assert.Equal(new DateOnly(2026, 10, 20), At("UTC", 2026, 10, 20, 23, 30).Today());
    }

    [Fact]
    public void A_timezone_ahead_of_utc_can_already_be_tomorrow()
    {
        // 23:30 UTC on 20 Oct is 12:30 on 21 Oct in Auckland (NZDT, UTC+13)
        Assert.Equal(new DateOnly(2026, 10, 21), At("Pacific/Auckland", 2026, 10, 20, 23, 30).Today());
    }

    [Fact]
    public void A_timezone_behind_utc_keeps_the_same_day_in_the_evening_utc()
    {
        // 23:30 UTC is 16:30 in Los Angeles (PDT, UTC-7)
        Assert.Equal(new DateOnly(2026, 10, 20), At("America/Los_Angeles", 2026, 10, 20, 23, 30).Today());
    }

    [Fact]
    public void A_timezone_behind_utc_can_still_be_yesterday()
    {
        // 06:30 UTC on 20 Oct is 23:30 on 19 Oct in Los Angeles
        Assert.Equal(new DateOnly(2026, 10, 19), At("America/Los_Angeles", 2026, 10, 20, 6, 30).Today());
    }

    [Fact]
    public void Unknown_timezone_falls_back_to_utc()
    {
        var clock = At("Not/A_Real_Zone", 2026, 10, 20, 23, 30);

        Assert.Equal(TimeZoneInfo.Utc, clock.TimeZone);
        Assert.Equal(new DateOnly(2026, 10, 20), clock.Today());
    }

    [Fact]
    public void Default_clock_returns_a_date_close_to_now()
    {
        var today = new Clock("UTC").Today();
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.True(Math.Abs(today.DayNumber - utcToday.DayNumber) <= 1);
    }
}
