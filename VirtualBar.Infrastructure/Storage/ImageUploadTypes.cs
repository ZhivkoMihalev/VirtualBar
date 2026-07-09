namespace VirtualBar.Infrastructure.Storage;

/// <summary>
/// Single source of truth for permitted image uploads. The stored file extension is derived from the
/// server-validated content type — never from the client-supplied <c>IFormFile.FileName</c> — so a caller
/// cannot persist a markup/executable extension (e.g. <c>.html</c>, <c>.svg</c>) under <c>wwwroot</c> and
/// have it served same-origin (stored XSS). SVG is intentionally not accepted; GIF is dropped.
/// </summary>
public static class ImageUploadTypes
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
        };

    /// <summary>Human-readable list of accepted formats, for validation messages.</summary>
    public const string AllowedFormatsLabel = "JPEG, PNG and WebP";

    /// <summary>True when <paramref name="contentType"/> is an accepted image content type.</summary>
    public static bool IsAllowed(string? contentType) =>
        contentType is not null && ExtensionByContentType.ContainsKey(contentType.Trim());

    /// <summary>
    /// Resolves the safe file extension (including the leading dot) for a server-validated content type.
    /// Returns <c>false</c> for anything not on the whitelist.
    /// </summary>
    public static bool TryGetExtension(string? contentType, out string extension)
    {
        if (contentType is not null && ExtensionByContentType.TryGetValue(contentType.Trim(), out var ext))
        {
            extension = ext;
            return true;
        }

        extension = string.Empty;
        return false;
    }
}
