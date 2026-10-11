using System.ComponentModel.DataAnnotations;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class AddBottleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public Guid? DistilleryId { get; set; }

    /// <summary>
    /// Optional catalog product picked from autocomplete. When null the add flow tries an exact
    /// canonical-key auto-link and otherwise files a pending catalog request.
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
    /// How many identical bottles the collector owns — one entry represents the whole stack.
    /// Omitting the field keeps the default of 1, so older clients need no change.
    /// </summary>
    public int Quantity { get; set; } = 1;

    public string? Description { get; set; }

    public bool IsLimited { get; set; }
}
