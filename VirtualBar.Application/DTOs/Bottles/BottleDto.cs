using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class BottleDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? DistilleryId { get; set; }

    public string? DistilleryName { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public SpiritCategory Category { get; set; }

    public int? Age { get; set; }

    public int? VintageYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public BottleCondition Condition { get; set; }

    public string? Description { get; set; }

    public bool IsLimited { get; set; }

    public bool IsForSale { get; set; }

    public decimal? AskingPrice { get; set; }

    public string? Currency { get; set; }

    public int LikesCount { get; set; }

    public int CommentsCount { get; set; }

    public string UserDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<BottleImageDto> Images { get; set; } = [];
}
