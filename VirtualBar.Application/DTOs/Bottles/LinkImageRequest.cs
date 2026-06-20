using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class LinkImageRequest
{
    [Required]
    public string Url { get; set; } = string.Empty;
}
