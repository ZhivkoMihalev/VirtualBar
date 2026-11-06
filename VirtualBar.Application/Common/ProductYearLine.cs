using System.Text.RegularExpressions;

namespace VirtualBar.Application.Common;

/// <summary>
/// The line of a dated catalog product (#59): its name with its own year taken out, so "Millésime 1988" and
/// "Millésime 1979" share the line "MILLÉSIME", and "Classic Cut 2020 Edition" belongs with "Classic Cut 2021 Edition".
/// Two products are in one line when they also share the category, the maker and the kind of year; this only
/// compares the names.
/// </summary>
public static class ProductYearLine
{
    /// <summary>The name without the given year as a whole number, spaces collapsed, upper-cased for comparing.</summary>
    public static string Of(string name, int year)
    {
        var withoutYear = Regex.Replace(name, $@"(?<!\d){year}(?!\d)", " ");
        return Regex.Replace(withoutYear, @"\s+", " ").Trim().ToUpperInvariant();
    }
}
