namespace VirtualBar.Application.DTOs.Products;

/// <summary>A collector who owns the product, with how many physical bottles of it they hold.</summary>
public sealed class ProductOwnerDto
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    /// <summary>Sum of <c>Quantity</c> over the user's linked bottles (every total counts physical bottles).</summary>
    public int BottleCount { get; set; }
}
