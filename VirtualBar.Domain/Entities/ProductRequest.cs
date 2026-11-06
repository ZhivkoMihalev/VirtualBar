using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public sealed class ProductRequest : BaseEntity
{
    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public Guid? DistilleryId { get; set; }

    public Distillery? Distillery { get; set; }

    public SpiritCategory Category { get; set; }

    public int? Age { get; set; }

    /// <summary>The requested year of distillation (#59); never set together with <see cref="ReleaseYear"/>.</summary>
    public int? VintageYear { get; set; }

    /// <summary>The requested year of a yearly edition (#59).</summary>
    public int? ReleaseYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public string? UserNote { get; set; }

    public string CanonicalKey { get; set; } = string.Empty;

    public ProductRequestStatus Status { get; set; } = ProductRequestStatus.Pending;

    public string? AdminNote { get; set; }

    public Guid? ResolvedProductId { get; set; }

    public Product? ResolvedProduct { get; set; }

    public Guid? SourceBottleId { get; set; }

    public DateTime? RespondedAt { get; set; }
}
