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

    // The lengths a GS1 number is written in: GTIN-8, GTIN-12 (UPC-A), GTIN-13 (EAN-13), GTIN-14.
    private static readonly int[] GtinLengths = [8, 12, 13, 14];

    /// <summary>
    /// Every way the same number can be written (#49): a GTIN keeps its value when zeros are added in front, so the UPC-A
    /// 082184090466 printed on a bottle is also the EAN-13 0082184090466 and the GTIN-14 00082184090466. A lookup and
    /// the pair check match on all of them, so a barcode stored in one form is found from another. The barcode as given
    /// (trimmed) is always one of them.
    /// </summary>
    public static IReadOnlyList<string> Equivalents(string barcode)
    {
        var trimmed = barcode.Trim();
        var digits = trimmed.TrimStart('0');

        return GtinLengths
            .Where(length => digits.Length <= length)
            .Select(length => digits.PadLeft(length, '0'))
            .Prepend(trimmed)
            .Distinct()
            .ToList();
    }
}
