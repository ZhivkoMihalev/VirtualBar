using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.WishList;

public sealed class AddWishListItemRequest
{
    public string? BottleName { get; set; }

    public Guid? DistilleryId { get; set; }

    public SpiritCategory? Category { get; set; }

    public string? ImageUrl { get; set; }
}
