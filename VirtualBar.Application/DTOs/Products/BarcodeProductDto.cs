namespace VirtualBar.Application.DTOs.Products;

public sealed class BarcodeProductDto
{
    /// <summary>
    /// Set only when the barcode resolved against the local catalog (L0), so the add form can link the product.
    /// </summary>
    public Guid? ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? ImageUrl { get; set; }

    public int? VolumeMl { get; set; }

    public double? AbvPercent { get; set; }
}
