namespace Pacer.Web.Services;

public static class UrlSafety
{
    /// <summary>Returns the URL only if it is a local path (blocks open redirects); otherwise the fallback.</summary>
    public static string LocalOrDefault(string? url, string fallback = "/")
    {
        if (string.IsNullOrEmpty(url)) return fallback;
        if (url[0] != '/') return fallback;
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\')) return fallback;
        return url;
    }
}

