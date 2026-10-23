using Pacer.Web.Models;

namespace Pacer.Web.Tests.Models;

public class PacingTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static Counter Make(int target, int current, DateOnly date) => new(1, "Test", target, current, date);

    [Fact]
    public void Active_counter_needs_remaining_spread_over_the_days_left()
    {
        // 28 days left, 70 to go: 2.5 a day, 17.5 a week
        var pace = Pacing.Compute(Make(100, 30, new DateOnly(2026, 11, 17)), Today);

        Assert.Equal(PaceStatus.Active, pace.Status);
        Assert.Equal(70, pace.Remaining);
        Assert.Equal(28, pace.DaysLeft);
        Assert.Equal(2.5, pace.PerDay!.Value, 6);
        Assert.Equal(17.5, pace.PerWeek!.Value, 6);
        Assert.Equal(30.0, pace.Percent, 6);
    }

    [Fact]
    public void One_week_left_means_per_week_equals_remaining()
    {
        var pace = Pacing.Compute(Make(50, 40, new DateOnly(2026, 10, 27)), Today);

        Assert.Equal(7, pace.DaysLeft);
        Assert.Equal(10.0, pace.PerWeek!.Value, 6);
    }

    [Fact]
    public void Per_week_can_be_fractional()
    {
        // 1 click left over 3 days: 7/3 per week
        var pace = Pacing.Compute(Make(10, 9, new DateOnly(2026, 10, 23)), Today);

        Assert.Equal(7.0 / 3.0, pace.PerWeek!.Value, 6);
        Assert.Equal(1.0 / 3.0, pace.PerDay!.Value, 6);
    }

    [Fact]
    public void Tomorrow_is_still_active_with_one_day_left()
    {
        var pace = Pacing.Compute(Make(10, 0, new DateOnly(2026, 10, 21)), Today);

        Assert.Equal(PaceStatus.Active, pace.Status);
        Assert.Equal(1, pace.DaysLeft);
        Assert.Equal(10.0, pace.PerDay!.Value, 6);
        Assert.Equal(70.0, pace.PerWeek!.Value, 6);
    }

    [Fact]
    public void Days_are_counted_across_a_year_boundary()
    {
        var pace = Pacing.Compute(Make(10, 0, new DateOnly(2027, 1, 5)), new DateOnly(2026, 12, 30));

        Assert.Equal(6, pace.DaysLeft);
    }

    [Fact]
    public void Target_date_today_is_due_today_with_no_rate()
    {
        var pace = Pacing.Compute(Make(100, 10, Today), Today);

        Assert.Equal(PaceStatus.DueToday, pace.Status);
        Assert.Equal(90, pace.Remaining);
        Assert.Equal(0, pace.DaysLeft);
        Assert.Null(pace.PerWeek);
        Assert.Null(pace.PerDay);
    }

    [Fact]
    public void Past_target_date_is_overdue_with_no_rate()
    {
        var pace = Pacing.Compute(Make(100, 10, new DateOnly(2026, 10, 19)), Today);

        Assert.Equal(PaceStatus.Overdue, pace.Status);
        Assert.Equal(-1, pace.DaysLeft);
        Assert.Equal(90, pace.Remaining);
        Assert.Null(pace.PerWeek);
        Assert.Null(pace.PerDay);
    }

    [Fact]
    public void Reaching_the_target_is_complete()
    {
        var pace = Pacing.Compute(Make(100, 100, new DateOnly(2026, 12, 1)), Today);

        Assert.Equal(PaceStatus.Complete, pace.Status);
        Assert.Equal(0, pace.Remaining);
        Assert.Equal(100.0, pace.Percent, 6);
        Assert.Null(pace.PerWeek);
    }

    [Fact]
    public void Complete_wins_over_overdue()
    {
        var pace = Pacing.Compute(Make(10, 10, new DateOnly(2026, 1, 1)), Today);

        Assert.Equal(PaceStatus.Complete, pace.Status);
    }

    [Fact]
    public void Counting_past_the_target_keeps_remaining_at_zero_and_percent_at_100()
    {
        var pace = Pacing.Compute(Make(100, 120, new DateOnly(2026, 12, 1)), Today);

        Assert.Equal(PaceStatus.Complete, pace.Status);
        Assert.Equal(0, pace.Remaining);
        Assert.Equal(100.0, pace.Percent, 6);
    }

    [Fact]
    public void Fresh_counter_is_zero_percent()
    {
        var pace = Pacing.Compute(Make(100, 0, new DateOnly(2026, 12, 1)), Today);

        Assert.Equal(0.0, pace.Percent, 6);
        Assert.Equal(100, pace.Remaining);
    }

    [Theory]
    [InlineData(25, 100, 25.0)]
    [InlineData(1, 3, 33.33)]
    [InlineData(99, 100, 99.0)]
    [InlineData(1, 1000, 0.1)]
    public void Percent_is_current_over_target(int current, int target, double expected)
    {
        var pace = Pacing.Compute(Make(target, current, new DateOnly(2026, 12, 1)), Today);

        Assert.Equal(expected, pace.Percent, 2);
    }
}
