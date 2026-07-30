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

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public string CanonicalKey { get; set; } = string.Empty;

    public ProductOrigin Origin { get; set; }

    public ICollection<Bottle> Bottles { get; set; } = [];
}
