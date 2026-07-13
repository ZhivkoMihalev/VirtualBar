using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public class Bottle : BaseEntity
{
    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public Guid? DistilleryId { get; set; }

    public Distillery? Distillery { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public SpiritCategory Category { get; set; }

    public int? Age { get; set; }

    public int? VintageYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public BottleCondition Condition { get; set; } = BottleCondition.Sealed;

    public string? Description { get; set; }

    public bool IsLimited { get; set; }

    public bool IsForSale { get; set; }

    public decimal? AskingPrice { get; set; }

    public string? Currency { get; set; }

    public DateTime? ForSaleAt { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<BottleImage> Images { get; set; } = [];

    public ICollection<BottleLike> Likes { get; set; } = [];

    public ICollection<BottleComment> Comments { get; set; } = [];
}
