using System.Xml.Linq;
using Pacer.Web.Data;

namespace Pacer.Web.Tests.Data;

public class EfXmlRepositoryTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void A_new_database_has_no_keys()
    {
        var repository = new EfXmlRepository(_database.Factory);

        Assert.Empty(repository.GetAllElements());
    }

    [Fact]
    public void Stored_keys_are_returned_in_the_order_they_were_stored()
    {
        var repository = new EfXmlRepository(_database.Factory);

        repository.StoreElement(new XElement("key", new XAttribute("id", "one")), "first");
        repository.StoreElement(new XElement("key", new XAttribute("id", "two")), "second");

        var ids = repository.GetAllElements().Select(e => (string?)e.Attribute("id")).ToArray();
        Assert.Equal(new[] { "one", "two" }, ids);
    }

    [Fact]
    public void Keys_are_shared_between_repository_instances()
    {
        new EfXmlRepository(_database.Factory)
            .StoreElement(new XElement("key", new XAttribute("id", "shared")), "shared");

        var seenByAnotherReplica = new EfXmlRepository(_database.Factory).GetAllElements();

        Assert.Equal("shared", (string?)Assert.Single(seenByAnotherReplica).Attribute("id"));
    }

    [Fact]
    public void Key_xml_survives_a_round_trip()
    {
        var repository = new EfXmlRepository(_database.Factory);
        var original = new XElement("key", new XAttribute("id", "abc"), new XElement("creationDate", "2026-10-21T00:00:00Z"));

        repository.StoreElement(original, "k");

        Assert.Equal(original.ToString(SaveOptions.DisableFormatting),
            Assert.Single(repository.GetAllElements()).ToString(SaveOptions.DisableFormatting));
    }
}
