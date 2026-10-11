using System.ComponentModel.DataAnnotations;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class UpdateBottleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public Guid? DistilleryId { get; set; }

    /// <summary>
    /// Catalog product link. Null unlinks the bottle; update never auto-links and never files a request.
    /// </summary>
    public Guid? ProductId { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public SpiritCategory Category { get; set; }

    public int? Age { get; set; }

    public int? VintageYear { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public BottleCondition Condition { get; set; } = BottleCondition.Sealed;

    /// <summary>
    /// How many identical bottles the collector owns. Omitting the field keeps the default of 1,
    /// so an older client that never sends it can never zero out an existing stack.
    /// </summary>
    public int Quantity { get; set; } = 1;

    public string? Description { get; set; }

    public bool IsLimited { get; set; }
}
