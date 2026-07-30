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

    public string? Description { get; set; }

    public bool IsLimited { get; set; }
}
