namespace VirtualBar.Application.Common;

/// <summary>
/// The barcode rule shared by bottles, product requests and the barcode lookup (#49). Spirits carry GS1 codes: EAN-8,
/// UPC-A (12), EAN-13 or ITF-14, so a barcode is 8 to 14 digits once trimmed.
/// </summary>
public static class BarcodeFormat
{
    public const int MinLength = 8;

    public const int MaxLength = 14;

    /// <summary>Whether <paramref name="barcode"/>, trimmed, is 8 to 14 ASCII digits.</summary>
    public static bool IsValid(string barcode)
    {
        var trimmed = barcode.Trim();

        return trimmed.Length is >= MinLength and <= MaxLength && trimmed.All(char.IsAsciiDigit);
    }

    /// <summary>The barcode as it is stored: trimmed, and null when nothing is left.</summary>
    public static string? Normalize(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
}
