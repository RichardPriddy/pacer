using Pacer.Web.Services;

namespace Pacer.Web.Tests.Services;

public class UrlSafetyTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/counters", "/counters")]
    [InlineData("/counters/5/edit", "/counters/5/edit")]
    [InlineData("/counters?x=1", "/counters?x=1")]
    public void Local_paths_are_kept(string url, string expected)
    {
        Assert.Equal(expected, UrlSafety.LocalOrDefault(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("counters")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("https://evil.example.com")]
    [InlineData("http://evil.example.com/path")]
    [InlineData("javascript:alert(1)")]
    public void Anything_that_is_not_a_local_path_falls_back(string? url)
    {
        Assert.Equal("/", UrlSafety.LocalOrDefault(url));
    }

    [Fact]
    public void Custom_fallback_is_used()
    {
        Assert.Equal("/counters", UrlSafety.LocalOrDefault("https://evil.example.com", "/counters"));
    }
}
