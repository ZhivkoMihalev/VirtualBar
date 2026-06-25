using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.WishList;

public sealed class WishListItemDto
{
    public Guid Id { get; set; }

    public string? BottleName { get; set; }

    public string? Distillery { get; set; }

    public SpiritCategory? Category { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
