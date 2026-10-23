namespace Pacer.Web.Data.Entities;

/// <summary>A signed-in user, keyed by their OIDC subject.</summary>
public class UserEntity
{
    public string Sub { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }

    /// <summary>The counter shown on the home page. Deliberately not a foreign key, to avoid a circular reference.</summary>
    public long? DefaultCounterId { get; set; }

    public List<CounterEntity> Counters { get; set; } = [];
}
