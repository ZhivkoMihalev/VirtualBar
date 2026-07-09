using System.Globalization;
using System.Text;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Common;

/// <summary>
/// Builds a deterministic, side-effect-free canonical key for a bottle so that the same product
/// added by different users collapses to a single cache row (and a single external lookup).
/// </summary>
/// <remarks>
/// <para>
/// The key is a pipe-delimited string with segments in a <b>stable order</b>:
/// <c>category | distillery | name | ageYo | vintage | volume</c>.
/// Optional segments (<c>distillery</c>, <c>ageYo</c>, <c>vintage</c>, <c>volume</c>) are
/// <b>omitted entirely</b> when their source value is null/empty, while the relative order of the
/// remaining segments never changes. The <c>category</c> and <c>name</c> segments are always present.
/// </para>
/// <para>Each text segment (category, distillery, name) is normalized identically:</para>
/// <list type="number">
///   <item><description>lower-cased using the invariant culture;</description></item>
///   <item><description>every character that is not an ASCII letter or digit is replaced with a single space (strips punctuation, apostrophes, hyphens, etc.);</description></item>
///   <item><description>runs of whitespace are collapsed to a single space and the result is trimmed;</description></item>
///   <item><description>the standalone noise word <c>the</c> is dropped (whole-word, any position).</description></item>
/// </list>
/// <para>Non-text segments are formatted as:</para>
/// <list type="bullet">
///   <item><description><c>ageYo</c>: the age followed by <c>yo</c>, e.g. <c>18yo</c>;</description></item>
///   <item><description><c>vintage</c>: the raw four-digit year, e.g. <c>1998</c>;</description></item>
///   <item><description><c>volume</c>: the raw millilitre count, e.g. <c>700</c>.</description></item>
/// </list>
/// <para>
/// Example: <c>For("The Macallan", "Sherry Oak", SpiritCategory.Whisky, 18, null, 700)</c>
/// produces <c>whisky|macallan|sherry oak|18yo|700</c>.
/// </para>
/// </remarks>
public static class ProductKey
{
    private const string Separator = "|";

    private const string NoiseWord = "the";

    /// <summary>
    /// Produces the canonical product key for the supplied bottle attributes.
    /// </summary>
    /// <param name="distilleryName">Optional distillery/brand name; the segment is omitted when null/blank.</param>
    /// <param name="name">The bottle/expression name; always contributes a (normalized) segment.</param>
    /// <param name="category">The spirit category; its lower-cased enum name is the first segment.</param>
    /// <param name="age">Optional age statement in years; emits an <c>Nyo</c> segment when present.</param>
    /// <param name="vintageYear">Optional vintage year; emits the raw year when present.</param>
    /// <param name="volumeMl">Optional volume in millilitres; emits the raw value when present.</param>
    /// <returns>A deterministic, lower-cased, punctuation-free, pipe-delimited key.</returns>
    public static string For(
        string? distilleryName,
        string name,
        SpiritCategory category,
        int? age,
        int? vintageYear,
        int? volumeMl)
    {
        var segments = new List<string>(6)
        {
            category.ToString().ToLowerInvariant(),
        };

        var distillery = Normalize(distilleryName);
        if (distillery.Length > 0)
            segments.Add(distillery);

        segments.Add(Normalize(name));

        if (age.HasValue)
            segments.Add($"{age.Value.ToString(CultureInfo.InvariantCulture)}yo");

        if (vintageYear.HasValue)
            segments.Add(vintageYear.Value.ToString(CultureInfo.InvariantCulture));

        if (volumeMl.HasValue)
            segments.Add(volumeMl.Value.ToString(CultureInfo.InvariantCulture));

        return string.Join(Separator, segments);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lowered = value.ToLowerInvariant();

        // Replace any non-alphanumeric character with a space to strip punctuation.
        var cleaned = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
            cleaned.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

        // Collapse whitespace and drop the "the" noise word.
        var tokens = cleaned
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token != NoiseWord);

        return string.Join(' ', tokens);
    }
}
