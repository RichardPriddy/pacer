using System.Globalization;

namespace Pacer.Web.Models;

public static class CounterValidator
{
    public const int MaxTarget = 10_000_000;
    public const int MaxNameLength = 80;

    public static (CounterInput? Input, List<string> Errors) Parse(
        string? name, string? target, string? targetDate, string? current, bool creating, DateOnly today)
    {
        var errors = new List<string>();

        name = name?.Trim() ?? "";
        if (name.Length == 0) errors.Add("Give the counter a name.");
        else if (name.Length > MaxNameLength) errors.Add($"Keep the name to {MaxNameLength} characters or fewer.");

        if (!int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) || t < 1 || t > MaxTarget)
            errors.Add($"Enter a count to between 1 and {MaxTarget.ToString("N0", CultureInfo.InvariantCulture)}.");

        if (!DateOnly.TryParseExact(targetDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            errors.Add("Choose a valid target date.");
        else if (creating && date < today)
            errors.Add("Choose a target date that is today or later.");

        var cur = 0;
        if (!creating && (!int.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out cur) || cur < 0))
            errors.Add("Current count must be 0 or more.");

        return errors.Count > 0 ? (null, errors) : (new CounterInput(name, t, date, cur), errors);
    }
}
