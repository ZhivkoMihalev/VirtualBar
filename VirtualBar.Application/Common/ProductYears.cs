using System.Text.RegularExpressions;

namespace VirtualBar.Application.Common;

/// <summary>
/// The rules for the year of a catalog product or a request for one (#59): a vintage year or a release year, never
/// both, from 1800 to next year, and shown in the name, since every dated product carries its year in its name.
/// </summary>
public static class ProductYears
{
    public const int Earliest = 1800;

    /// <summary>Next year: an edition can be announced before its year starts.</summary>
    public static int Latest => DateTime.UtcNow.Year + 1;

    /// <summary>Whether <paramref name="year"/> appears in <paramref name="name"/> as a whole number.</summary>
    public static bool InName(string name, int year) => Regex.IsMatch(name, $@"(?<!\d){year}(?!\d)");

    /// <summary>Why these years cannot go with this name, or null when they can (no year at all is fine).</summary>
    public static string? Problem(string name, int? vintageYear, int? releaseYear)
    {
        if (vintageYear is not null && releaseYear is not null)
            return "A product has a vintage year or a release year, not both.";

        if ((vintageYear ?? releaseYear) is not int year)
            return null;

        if (year < Earliest || year > Latest)
            return $"Year must be between {Earliest} and {Latest}.";

        if (!InName(name, year))
            return "The name must contain the year.";

        return null;
    }

    /// <summary>
    /// The years an approval gives the product: an override of either year replaces the request's two together, so an
    /// admin can turn a requested vintage into an edition; with no override, the request's years stand.
    /// </summary>
    public static (int? VintageYear, int? ReleaseYear) Effective(
        int? overrideVintageYear, int? overrideReleaseYear, int? vintageYear, int? releaseYear) =>
        overrideVintageYear is not null || overrideReleaseYear is not null
            ? (overrideVintageYear, overrideReleaseYear)
            : (vintageYear, releaseYear);
}
