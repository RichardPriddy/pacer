namespace Pacer.Web.Data.Entities;

public class CounterEntity
{
    public long Id { get; set; }
    public string UserSub { get; set; } = "";
    public UserEntity User { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Target { get; set; }
    public int CurrentCount { get; set; }
    public DateOnly TargetDate { get; set; }
}
