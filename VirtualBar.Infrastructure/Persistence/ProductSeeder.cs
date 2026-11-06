using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualBar.Application.Common;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Infrastructure.Persistence;

public static class ProductSeeder
{
    private const string ResourcePrefix = "VirtualBar.Infrastructure.Persistence.SeedData.products.";

    private const string ResourceSuffix = ".seed.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Tops up the catalog from every embedded per-category seed file
    /// (<c>products.&lt;category&gt;.seed.json</c>). Files are read in a stable ordinal order so that a
    /// canonical key present in more than one file always resolves to the same row.
    /// </summary>
    /// <param name="logger">
    /// Optional. When supplied, a warning is written for any <c>distillery</c> value the seed names that
    /// <see cref="DistillerySeeder"/> does not know — those rows silently lose their foreign key.
    /// </param>
    public static async Task SeedProductsAsync(
        AppDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var assembly = typeof(ProductSeeder).Assembly;

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (resourceNames.Count == 0)
            throw new InvalidOperationException(
                $"No seed resources matching '{ResourcePrefix}*{ResourceSuffix}' were found.");

        var unresolvedDistilleries = new List<string>();

        foreach (var resourceName in resourceNames)
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Seed resource '{resourceName}' not found.");

            await SeedFromStreamAsync(db, stream, cancellationToken, unresolvedDistilleries);
        }

        if (unresolvedDistilleries.Count == 0 || logger is null)
            return;

        var distinct = unresolvedDistilleries
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        logger.LogWarning(
            "Product seed: {RowCount} row(s) named {NameCount} distillery/distilleries that are missing from " +
            "DistillerySeeder. They were stored as Brand with a null DistilleryId, so they will not appear in " +
            "/api/distilleries, the add-bottle picker or wish-list matching. Most frequent: {Names}",
            unresolvedDistilleries.Count,
            distinct.Count,
            string.Join(", ", distinct.Take(15).Select(g => $"{g.Key} ({g.Count()})")));
    }

    /// <summary>
    /// Tops up the catalog from an already-opened seed stream: rows whose <c>CanonicalKey</c> is not yet
    /// present are inserted; existing rows are never re-inserted or deleted. The one update: a live existing
    /// product with neither year gets the <c>VintageYear</c> or <c>ReleaseYear</c> its seed row carries, and no other
    /// field (#59, approved by the owner); a product that already has a year, or is soft-deleted, is left as it is.
    /// The public entry point owns the embedded resources; this overload keeps the row-level rules (category parsing,
    /// distillery resolution, canonical-key dedupe) reachable in isolation.
    /// </summary>
    /// <param name="unresolvedDistilleries">
    /// Optional sink collecting every <c>distillery</c> value that did not match a seeded distillery and was
    /// therefore demoted to <c>Brand</c>. Lets the caller report a silent data gap instead of hiding it.
    /// </param>
    internal static async Task SeedFromStreamAsync(
        AppDbContext db,
        Stream stream,
        CancellationToken cancellationToken,
        ICollection<string>? unresolvedDistilleries = null)
    {
        var rows = await JsonSerializer.DeserializeAsync<List<ProductSeedRow>>(
            stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Seed payload deserialized to null.");

        var distilleryIdByName = await db.Distilleries
            .AsNoTracking()
            .Select(d => new { d.Name, d.Id })
            .ToDictionaryAsync(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var now = DateTime.UtcNow;

        var products = new List<Product>(rows.Count);

        foreach (var row in rows)
        {
            if (!Enum.TryParse<SpiritCategory>(row.Category, ignoreCase: true, out var category))
                continue;

            Guid? distilleryId = null;
            string? brand = row.Brand;

            // The producer segment of the canonical key is the distillery text whenever the row names
            // one, resolved or not. Keeping it separate from Brand means an unknown distillery no
            // longer overwrites the label the row actually carries, while the key stays byte-identical
            // — and it must, because ProductKey is frozen and shared with the PriceSnapshot cache.
            string? keyProducer = null;

            if (!string.IsNullOrWhiteSpace(row.Distillery))
            {
                keyProducer = row.Distillery;

                if (distilleryIdByName.TryGetValue(row.Distillery, out var matchedId))
                {
                    distilleryId = matchedId;
                }
                else
                {
                    brand ??= row.Distillery;
                    unresolvedDistilleries?.Add(row.Distillery);
                }
            }

            var canonicalKey = ProductKey.For(
                keyProducer ?? brand,
                row.Name,
                category,
                row.Age,
                null,
                row.VolumeMl);

            products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = row.Name,
                Brand = brand,
                DistilleryId = distilleryId,
                Category = category,
                Country = row.Country,
                Region = row.Region,
                Age = row.Age,
                VintageYear = row.VintageYear,
                ReleaseYear = row.ReleaseYear,
                AbvPercent = row.AbvPercent,
                VolumeMl = row.VolumeMl,
                Barcode = row.Barcode,
                ImageUrl = row.ImageUrl,
                Description = row.Description,
                CanonicalKey = canonicalKey,
                Origin = ProductOrigin.Seeded,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var deduped = products
            .GroupBy(p => p.CanonicalKey)
            .Select(g => g.First())
            .ToList();

        var existingKeys = (await db.Products
            .Select(p => p.CanonicalKey)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var missing = deduped
            .Where(p => !existingKeys.Contains(p.CanonicalKey))
            .ToList();

        db.Products.AddRange(missing);

        var filled = await FillYearsAsync(db, deduped, existingKeys, now, cancellationToken);

        if (missing.Count > 0 || filled > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gives the existing products that have no year yet the year their seed row carries (#59). Rows seeded before
    /// the years existed would otherwise never get one, since the seed only inserts. Returns how many it filled.
    /// </summary>
    private static async Task<int> FillYearsAsync(
        AppDbContext db,
        List<Product> seeded,
        HashSet<string> existingKeys,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var yearByKey = seeded
            .Where(p => (p.VintageYear ?? p.ReleaseYear) is not null && existingKeys.Contains(p.CanonicalKey))
            .ToDictionary(p => p.CanonicalKey);

        if (yearByKey.Count == 0)
            return 0;

        var keys = yearByKey.Keys.ToList();
        var undated = await db.Products
            .Where(p => !p.IsDeleted && p.VintageYear == null && p.ReleaseYear == null && keys.Contains(p.CanonicalKey))
            .ToListAsync(cancellationToken);

        foreach (var product in undated)
        {
            var seed = yearByKey[product.CanonicalKey];
            product.VintageYear = seed.VintageYear;
            product.ReleaseYear = seed.ReleaseYear;
            product.UpdatedAt = now;
        }

        return undated.Count;
    }

    private sealed record ProductSeedRow
    {
        public string Name { get; init; } = string.Empty;

        public string? Brand { get; init; }

        public string? Distillery { get; init; }

        public string Category { get; init; } = string.Empty;

        public string? Country { get; init; }

        public string? Region { get; init; }

        public int? Age { get; init; }

        public int? VintageYear { get; init; }

        public int? ReleaseYear { get; init; }

        public double? AbvPercent { get; init; }

        public int? VolumeMl { get; init; }

        public string? Barcode { get; init; }

        public string? ImageUrl { get; init; }

        public string? Description { get; init; }
    }
}
