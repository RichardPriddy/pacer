namespace Pacer.Web.Data.Entities;

/// <summary>ASP.NET Core data protection keys, shared by every replica.</summary>
public class DataProtectionKeyEntity
{
    public long Id { get; set; }
    public string? FriendlyName { get; set; }
    public string Xml { get; set; } = "";
}
