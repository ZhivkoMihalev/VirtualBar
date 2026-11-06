using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Products;

public sealed class ProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public Guid? DistilleryId { get; set; }

    public string? DistilleryName { get; set; }

    public SpiritCategory Category { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public int? Age { get; set; }

    /// <summary>The year of distillation of a vintage-dated product (#59); a bottle linked to it takes it as its vintage.</summary>
    public int? VintageYear { get; set; }

    /// <summary>The year of a yearly edition (#59); never set together with <see cref="VintageYear"/>.</summary>
    public int? ReleaseYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? ImageUrl { get; set; }
}
