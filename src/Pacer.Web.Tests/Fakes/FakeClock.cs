using Pacer.Web.Services;

namespace Pacer.Web.Tests.Fakes;

public sealed class FakeClock(DateOnly today) : IClock
{
    public DateOnly Date { get; set; } = today;

    public DateOnly Today() => Date;
}
