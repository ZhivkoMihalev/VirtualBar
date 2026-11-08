namespace VirtualBar.Application.DTOs.Products;

public sealed class BarcodeProductDto
{
    /// <summary>
    /// The catalog product the barcode belongs to, so the add form can link it. Every answer is a catalog product (#49).
    /// </summary>
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? ImageUrl { get; set; }

    public int? VolumeMl { get; set; }

    public double? AbvPercent { get; set; }
}
