using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Products;

/// <summary>
/// What other collectors' bottles with a barcode say about it, when no catalog product has it (#49): each field is the
/// value most of those bottles carry. A suggestion for the add form to check, not a product: it has no id.
/// </summary>
public sealed class BarcodeSuggestionDto
{
    public string Name { get; set; } = string.Empty;

    public SpiritCategory Category { get; set; }

    public Guid? DistilleryId { get; set; }

    public string? DistilleryName { get; set; }

    public int? Age { get; set; }

    public int? VintageYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }
}
