namespace VirtualBar.Application.DTOs.ProductBarcodes;

/// <summary>A barcode collectors gave a catalog product (#49), as the admin list shows it.</summary>
public sealed class ProductBarcodeDto
{
    public Guid Id { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>The product's brand, or its non-deleted distillery's name when it has none.</summary>
    public string? ProductBrand { get; set; }

    public Guid AddedByUserId { get; set; }

    public string AddedByDisplayName { get; set; } = string.Empty;

    /// <summary>How many collectors back the pair: who recorded it, and whose live bottles carry it.</summary>
    public int Collectors { get; set; }

    /// <summary>Whether the barcode, in any of its forms, also belongs to another live product.</summary>
    public bool Conflict { get; set; }

    public DateTime CreatedAt { get; set; }
}
