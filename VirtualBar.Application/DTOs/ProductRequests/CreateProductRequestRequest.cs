using System.ComponentModel.DataAnnotations;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.ProductRequests;

public sealed class CreateProductRequestRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public Guid? DistilleryId { get; set; }

    public SpiritCategory Category { get; set; }

    public int? Age { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public string? UserNote { get; set; }

    /// <summary>
    /// The bottle whose add triggered this request. Set by the add-bottle flow only; controller clients
    /// leave it null. When present it must belong to the current collector.
    /// </summary>
    public Guid? SourceBottleId { get; set; }
}
