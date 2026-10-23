using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data.Entities;
using System.Xml.Linq;

namespace Pacer.Web.Data;

/// <summary>
/// Stores ASP.NET Core data protection keys in the database so that every replica can read
/// each other's cookies, and sessions survive restarts and redeploys.
/// </summary>
public sealed class EfXmlRepository(IDbContextFactory<PacerDbContext> factory) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var db = factory.CreateDbContext();
        var xml = db.DataProtectionKeys.AsNoTracking().OrderBy(k => k.Id).Select(k => k.Xml).ToList();
        return xml.Select(x => XElement.Parse(x)).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var db = factory.CreateDbContext();
        db.DataProtectionKeys.Add(new DataProtectionKeyEntity
        {
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
        });
        db.SaveChanges();
    }
}
