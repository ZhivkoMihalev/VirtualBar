using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class ListForSaleRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Asking price must be greater than zero.")]
    public decimal AskingPrice { get; set; }

    [Required]
    public string Currency { get; set; } = string.Empty;
}
