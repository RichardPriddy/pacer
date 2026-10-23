using Pacer.Web.Models;

namespace Pacer.Web.Tests.Models;

public class FmtTests
{
    [Fact]
    public void Num_of_null_is_empty()
    {
        Assert.Equal("", Fmt.Num(null));
    }

    [Theory]
    [InlineData(17.5, "17.5")]
    [InlineData(17.0, "17")]
    [InlineData(17.04, "17")]
    [InlineData(2.34, "2.3")]
    [InlineData(2.36, "2.4")]
    [InlineData(0.0, "0")]
    [InlineData(0.04, "0")]
    [InlineData(1234.5, "1,234.5")]
    [InlineData(1234567.0, "1,234,567")]
    public void Num_rounds_to_one_decimal_and_drops_a_trailing_zero(double value, string expected)
    {
        Assert.Equal(expected, Fmt.Num(value));
    }

    [Theory]
    [InlineData(0, "0 days")]
    [InlineData(1, "1 day")]
    [InlineData(-1, "1 day")]
    [InlineData(2, "2 days")]
    [InlineData(-3, "3 days")]
    [InlineData(30, "30 days")]
    public void Days_uses_singular_and_plural_and_ignores_sign(int n, string expected)
    {
        Assert.Equal(expected, Fmt.Days(n));
    }

    [Fact]
    public void Date_is_day_month_name_year()
    {
        Assert.Equal("5 Mar 2027", Fmt.Date(new DateOnly(2027, 3, 5)));
        Assert.Equal("31 Dec 2026", Fmt.Date(new DateOnly(2026, 12, 31)));
    }

    [Theory]
    [InlineData(33.3333, "33.33")]
    [InlineData(100.0, "100.00")]
    [InlineData(0.0, "0.00")]
    [InlineData(7.5, "7.50")]
    public void Percent_has_two_decimals_and_a_dot_separator(double value, string expected)
    {
        Assert.Equal(expected, Fmt.Percent(value));
    }
}
