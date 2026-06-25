using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public sealed class WishListItem : BaseEntity
{
    public Guid UserId { get; set; }

    public string? BottleName { get; set; }

    public Guid? DistilleryId { get; set; }

    public Distillery? Distillery { get; set; }

    public SpiritCategory? Category { get; set; }

    public string? ImageUrl { get; set; }

    public AppUser User { get; set; } = null!;
}
