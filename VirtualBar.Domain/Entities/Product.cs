using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public sealed class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public Guid? DistilleryId { get; set; }

    public Distillery? Distillery { get; set; }

    public SpiritCategory Category { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public int? Age { get; set; }

    /// <summary>
    /// The year the spirit was distilled or harvested, for a vintage-dated product (Frapin Millésime 1988). A bottle
    /// linked to the product takes it as its own vintage. Never set together with <see cref="ReleaseYear"/>.
    /// </summary>
    public int? VintageYear { get; set; }

    /// <summary>
    /// The year of a yearly edition (Lagavulin 12 Year Old Special Release 2021): it links the editions of one series,
    /// but is not the year of distillation, so a bottle never takes it. Never set together with
    /// <see cref="VintageYear"/>.
    /// </summary>
    public int? ReleaseYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public string CanonicalKey { get; set; } = string.Empty;

    public ProductOrigin Origin { get; set; }

    public ICollection<Bottle> Bottles { get; set; } = [];
}
