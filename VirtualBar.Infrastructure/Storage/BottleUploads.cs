using Microsoft.AspNetCore.Hosting;

namespace VirtualBar.Infrastructure.Storage;

/// <summary>
/// The folder bottle images are uploaded to, and the safe way to turn a stored image URL back into a file. A URL
/// resolves only when it names a file inside that folder, so a URL with <c>..</c>, another folder or a rooted path never
/// reaches a file anywhere else (#134).
/// </summary>
public static class BottleUploads
{
    /// <summary>The URL prefix of every uploaded bottle image.</summary>
    public const string UrlPrefix = "/uploads/bottles/";

    /// <summary>
    /// The full path of the uploaded file <paramref name="url"/> names, or null when the URL is not an upload of the
    /// bottle folder or would resolve outside it.
    /// </summary>
    public static string? FilePathOf(IWebHostEnvironment env, string url)
    {
        if (!url.StartsWith(UrlPrefix, StringComparison.Ordinal))
            return null;

        var directory = Path.GetFullPath(Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "bottles"));
        var filePath = Path.GetFullPath(Path.Combine(directory, url[UrlPrefix.Length..]));

        return filePath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? filePath : null;
    }
}
