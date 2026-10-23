namespace Pacer.Web.Models;

public sealed record Counter(long Id, string Name, int Target, int Current, DateOnly TargetDate);

public sealed record CounterInput(string Name, int Target, DateOnly TargetDate, int Current);

/// <summary>A counter together with its pace, ready to display.</summary>
public sealed record CounterView(Counter C, Pace S, bool IsDefault);

/// <summary>Raw text from the counter form; validated by <see cref="CounterValidator"/>.</summary>
public sealed class CounterFormValues
{
    public string? Name { get; set; }
    public string? Target { get; set; }
    public string? TargetDate { get; set; }
    public string? Current { get; set; }
}
