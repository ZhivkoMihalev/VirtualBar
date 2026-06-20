namespace VirtualBar.Application.DTOs.Products;

public sealed class BarcodeProductDto
{
    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? ImageUrl { get; set; }

    public int? VolumeMl { get; set; }

    public double? AbvPercent { get; set; }
}
