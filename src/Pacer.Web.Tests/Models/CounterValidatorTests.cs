using Pacer.Web.Models;

namespace Pacer.Web.Tests.Models;

public class CounterValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static (CounterInput? Input, List<string> Errors) Create(string? name, string? target, string? date) =>
        CounterValidator.Parse(name, target, date, null, creating: true, Today);

    private static (CounterInput? Input, List<string> Errors) Edit(string? name, string? target, string? date, string? current) =>
        CounterValidator.Parse(name, target, date, current, creating: false, Today);

    // ---- creating ----

    [Fact]
    public void Valid_create_returns_the_parsed_input_and_no_errors()
    {
        var (input, errors) = Create("Pages", "500", "2026-12-31");

        Assert.Empty(errors);
        Assert.NotNull(input);
        Assert.Equal("Pages", input!.Name);
        Assert.Equal(500, input.Target);
        Assert.Equal(new DateOnly(2026, 12, 31), input.TargetDate);
        Assert.Equal(0, input.Current);
    }

    [Fact]
    public void Name_is_trimmed()
    {
        var (input, _) = Create("  Pages read  ", "500", "2026-12-31");

        Assert.Equal("Pages read", input!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string? name)
    {
        var (input, errors) = Create(name, "500", "2026-12-31");

        Assert.Null(input);
        Assert.Equal("Give the counter a name.", Assert.Single(errors));
    }

    [Fact]
    public void Name_of_80_characters_is_allowed()
    {
        var (input, errors) = Create(new string('a', 80), "5", "2026-12-31");

        Assert.Empty(errors);
        Assert.NotNull(input);
    }

    [Fact]
    public void Name_of_81_characters_is_rejected()
    {
        var (input, errors) = Create(new string('a', 81), "5", "2026-12-31");

        Assert.Null(input);
        Assert.Equal("Keep the name to 80 characters or fewer.", Assert.Single(errors));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("10000001")]
    [InlineData("99999999999")]
    public void Invalid_target_is_rejected(string? target)
    {
        var (input, errors) = Create("Pages", target, "2026-12-31");

        Assert.Null(input);
        Assert.Contains("between 1 and 10,000,000", Assert.Single(errors));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10000000", 10_000_000)]
    [InlineData(" 5 ", 5)]
    public void Boundary_and_padded_targets_are_accepted(string target, int expected)
    {
        var (input, errors) = Create("Pages", target, "2026-12-31");

        Assert.Empty(errors);
        Assert.Equal(expected, input!.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("2026-13-01")]
    [InlineData("20/10/2026")]
    [InlineData("2026-02-30")]
    public void Invalid_date_is_rejected(string? date)
    {
        var (input, errors) = Create("Pages", "5", date);

        Assert.Null(input);
        Assert.Equal("Choose a valid target date.", Assert.Single(errors));
    }

    [Fact]
    public void Creating_with_a_past_date_is_rejected()
    {
        var (input, errors) = Create("Pages", "5", "2026-10-19");

        Assert.Null(input);
        Assert.Contains("today or later", Assert.Single(errors));
    }

    [Fact]
    public void Creating_with_todays_date_is_allowed()
    {
        var (input, errors) = Create("Pages", "5", "2026-10-20");

        Assert.Empty(errors);
        Assert.Equal(Today, input!.TargetDate);
    }

    [Fact]
    public void Current_is_ignored_when_creating()
    {
        var (input, errors) = CounterValidator.Parse("Pages", "5", "2026-12-31", "not a number", creating: true, Today);

        Assert.Empty(errors);
        Assert.Equal(0, input!.Current);
    }

    [Fact]
    public void Every_problem_is_reported_at_once_when_creating()
    {
        var (input, errors) = Create("", "0", "nope");

        Assert.Null(input);
        Assert.Equal(3, errors.Count);
    }

    // ---- editing ----

    [Fact]
    public void Valid_edit_includes_the_current_count()
    {
        var (input, errors) = Edit("Pages", "500", "2026-12-31", "12");

        Assert.Empty(errors);
        Assert.Equal(12, input!.Current);
    }

    [Fact]
    public void Current_of_zero_is_allowed_when_editing()
    {
        var (input, errors) = Edit("Pages", "500", "2026-12-31", "0");

        Assert.Empty(errors);
        Assert.Equal(0, input!.Current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("-1")]
    public void Invalid_current_is_rejected_when_editing(string? current)
    {
        var (input, errors) = Edit("Pages", "500", "2026-12-31", current);

        Assert.Null(input);
        Assert.Equal("Current count must be 0 or more.", Assert.Single(errors));
    }

    [Fact]
    public void A_past_date_is_allowed_when_editing()
    {
        var (input, errors) = Edit("Pages", "500", "2020-01-01", "3");

        Assert.Empty(errors);
        Assert.Equal(new DateOnly(2020, 1, 1), input!.TargetDate);
    }

    [Fact]
    public void Every_problem_is_reported_at_once_when_editing()
    {
        var (input, errors) = Edit("", "0", "nope", "-1");

        Assert.Null(input);
        Assert.Equal(4, errors.Count);
    }
}
