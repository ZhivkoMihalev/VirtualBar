using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VirtualBar.Application.Common;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Tests.Services;

/// <summary>
/// Guards the committed seed files themselves, not the seeding code. These invariants are cheap to
/// break by hand-editing JSON and expensive to notice at runtime, because <see cref="ProductSeeder"/>
/// silently demotes an unknown distillery to a brand rather than failing.
/// </summary>
public sealed class ProductSeedDataTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// The category a file claims by its name: <c>…SeedData.products.whisky.seed.json</c>. Each row also
    /// carries its own category field, and the seeder trusts that one — so a row filed under the wrong
    /// name still seeds correctly and nothing ever complains. It is still a curation error worth failing
    /// on, because the files are edited by hand and a misfiled row is invisible otherwise.
    /// </summary>
    private static SpiritCategory? CategoryFromResourceName(string resource)
    {
        var parts = resource.Split('.');

        return parts.Length >= 3 && Enum.TryParse<SpiritCategory>(parts[^3], ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Exactly how <see cref="ProductSeeder"/> builds the key: the distillery names the producer
    /// whenever the row has one, and the brand stands in only when it does not.</summary>
    private static string CanonicalKeyOf(SeedRow row) =>
        ProductKey.For(
            string.IsNullOrWhiteSpace(row.Distillery) ? row.Brand : row.Distillery,
            row.Name,
            row.Category,
            row.Age,
            null,
            row.VolumeMl);

    private static IReadOnlyList<(string Resource, SeedRow Row)> LoadSeedRows()
    {
        var assembly = typeof(ProductSeeder).Assembly;
        var rows = new List<(string, SeedRow)>();

        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".SeedData.products.", StringComparison.Ordinal)
                && n.EndsWith(".seed.json", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            var parsed = JsonSerializer.Deserialize<List<SeedRow>>(stream, SerializerOptions)!;

            foreach (var row in parsed)
                rows.Add((resource, row));
        }

        return rows;
    }

    [Fact]
    public void SeedFiles_AreEmbeddedAndNonEmpty()
    {
        var rows = LoadSeedRows();

        Assert.True(rows.Count > 200, $"Expected the curated catalog to be embedded, got {rows.Count} rows.");
    }

    /// <summary>
    /// Brand is what the label says and every bottle has one; Distillery is where it was made and is
    /// only known for some. They are separate facts, not two ways of writing the same one — a gin can
    /// carry its own brand while being distilled somewhere else entirely.
    /// </summary>
    [Fact]
    public void EverySeedRow_CarriesABrand()
    {
        var offenders = LoadSeedRows()
            .Where(x => string.IsNullOrWhiteSpace(x.Row.Brand))
            .Select(x => $"{x.Row.Category} | {x.Row.Name} | distillery='{x.Row.Distillery}'")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Every product must name the brand on its label. {offenders.Count} row(s) have none:\n"
            + string.Join("\n", offenders.Take(30)));
    }

    /// <summary>
    /// Every <c>distillery</c> in the seed must exist in <see cref="DistillerySeeder"/>. When it does not,
    /// the seeder quietly stores the name as a brand with a null FK, so the product never shows up under
    /// its distillery in <c>/api/distilleries</c>, the add-bottle picker or wish-list matching.
    /// </summary>
    [Fact]
    public void EverySeedDistillery_ExistsInDistillerySeeder()
    {
        var known = DistillerySeeder.KnownDistilleries
            .Select(d => d.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unresolved = LoadSeedRows()
            .Where(x => !string.IsNullOrWhiteSpace(x.Row.Distillery) && !known.Contains(x.Row.Distillery!))
            .GroupBy(x => x.Row.Distillery!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unresolved.Count == 0)
            return;

        var rowCount = unresolved.Sum(g => g.Count());
        var message = new StringBuilder();

        message.AppendLine(
            $"{rowCount} seed row(s) reference {unresolved.Count} distillery name(s) that DistillerySeeder does not know.");
        message.AppendLine("Either add the name to DistillerySeeder (if it really is a distillery) or move it");
        message.AppendLine("to the 'brand' field in the seed file (if it is a brand, range or product line).");
        message.AppendLine();

        foreach (var group in unresolved)
        {
            var categories = group.Select(x => x.Row.Category).Distinct().OrderBy(c => c.ToString());
            message.AppendLine($"  {group.Count(),4}  {group.Key}  [{string.Join(", ", categories)}]");
        }

        Assert.Fail(message.ToString());
    }

    /// <summary>
    /// The one that actually bites. <see cref="ProductSeeder"/> is insert-only by <c>CanonicalKey</c>, so
    /// the second row sharing a key is skipped in silence — no error, no log, it simply never reaches the
    /// catalog. Hand-curating thousands of rows across eight files makes that easy to do and impossible
    /// to notice; the key is also frozen (shared with the PriceSnapshot cache), so this cannot be fixed
    /// later by changing how it is computed.
    /// </summary>
    [Fact]
    public void NoTwoSeedRows_ShareACanonicalKey()
    {
        var collisions = LoadSeedRows()
            .GroupBy(x => CanonicalKeyOf(x.Row), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (collisions.Count == 0)
            return;

        var lost = collisions.Sum(g => g.Count() - 1);
        var message = new StringBuilder();

        message.AppendLine(
            $"{collisions.Count} canonical key(s) are claimed by more than one seed row; {lost} row(s) would");
        message.AppendLine("never be seeded. Give the duplicates something that distinguishes them (volume, age)");
        message.AppendLine("or delete the redundant one.");
        message.AppendLine();

        foreach (var group in collisions.Take(30))
        {
            message.AppendLine($"  {group.Key}");

            foreach (var (_, row) in group)
                message.AppendLine($"      {row.Category} | {row.Name} | {row.VolumeMl} ml");
        }

        Assert.Fail(message.ToString());
    }

    [Fact]
    public void EverySeedRow_SitsInTheFileOfItsOwnCategory()
    {
        var offenders = LoadSeedRows()
            .Select(x => (Expected: CategoryFromResourceName(x.Resource), x.Row))
            .Where(x => x.Expected is { } expected && expected != x.Row.Category)
            .Select(x => $"{x.Row.Name} is {x.Row.Category} but sits in the {x.Expected} file")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} row(s) are filed under the wrong category:\n" + string.Join("\n", offenders.Take(30)));
    }

    /// <summary>
    /// The same bounds <c>ProductRequestValidationDecorator</c> enforces on user-filed requests. A seeded
    /// row bypasses that decorator entirely, so nothing else would ever catch a typo like 430% ABV.
    /// </summary>
    [Fact]
    public void EverySeedRow_HasNumbersWithinTheSameBoundsUserRequestsMustMeet()
    {
        var offenders = new List<string>();

        foreach (var (_, row) in LoadSeedRows())
        {
            if (row.AbvPercent is not null and (< 1 or > 96))
                offenders.Add($"{row.Name}: abvPercent {row.AbvPercent} outside 1-96");

            if (row.Age is not null and (< 1 or > 100))
                offenders.Add($"{row.Name}: age {row.Age} outside 1-100");

            if (row.VolumeMl is not null and (< 20 or > 6000))
                offenders.Add($"{row.Name}: volumeMl {row.VolumeMl} outside 20-6000");

            if (row.Barcode is not null
                && (row.Barcode.Length is < 8 or > 14 || !row.Barcode.All(char.IsDigit)))
            {
                offenders.Add($"{row.Name}: barcode '{row.Barcode}' is not 8-14 digits");
            }
        }

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} row(s) carry out-of-range values:\n" + string.Join("\n", offenders.Take(30)));
    }

    private sealed record SeedRow
    {
        public string Name { get; init; } = string.Empty;

        public string? Brand { get; init; }

        public string? Distillery { get; init; }

        public SpiritCategory Category { get; init; }

        public int? Age { get; init; }

        public double? AbvPercent { get; init; }

        public int? VolumeMl { get; init; }

        public string? Barcode { get; init; }
    }
}
