using System.Globalization;

namespace Pacer.Web.Models;

public enum PaceStatus { Active, DueToday, Overdue, Complete }

public sealed record Pace(int Remaining, double Percent, int DaysLeft, PaceStatus Status, double? PerWeek, double? PerDay);

public static class Pacing
{
    /// <summary>Works out how fast the counter has to move to reach its target in time.</summary>
    public static Pace Compute(Counter c, DateOnly today)
    {
        var remaining = Math.Max(c.Target - c.Current, 0);
        var percent = c.Target > 0 ? Math.Min(c.Current * 100.0 / c.Target, 100.0) : 0.0;
        var daysLeft = c.TargetDate.DayNumber - today.DayNumber;

        var status =
            remaining == 0 ? PaceStatus.Complete :
            daysLeft < 0 ? PaceStatus.Overdue :
            daysLeft == 0 ? PaceStatus.DueToday :
            PaceStatus.Active;

        double? perDay = null;
        double? perWeek = null;
        if (status == PaceStatus.Active)
        {
            perDay = (double)remaining / daysLeft;
            perWeek = perDay * 7;
        }

        return new Pace(remaining, percent, daysLeft, status, perWeek, perDay);
    }
}

public static class Fmt
{
    public static string Num(double? value)
    {
        if (value is null) return "";
        var rounded = Math.Round(value.Value, 1);
        return rounded == Math.Floor(rounded)
            ? rounded.ToString("N0", CultureInfo.InvariantCulture)
            : rounded.ToString("N1", CultureInfo.InvariantCulture);
    }

    public static string Date(DateOnly d) => d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string Days(int n) => Math.Abs(n) == 1 ? $"{Math.Abs(n)} day" : $"{Math.Abs(n)} days";

    public static string Percent(double p) => p.ToString("0.00", CultureInfo.InvariantCulture);
}
