using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class ReorderBottlesRequest
{
    [Required]
    public List<Guid> BottleIds { get; set; } = [];
}
