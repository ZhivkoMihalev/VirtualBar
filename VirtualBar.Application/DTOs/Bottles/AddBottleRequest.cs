using System.ComponentModel.DataAnnotations;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class AddBottleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Distillery { get; set; }

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
