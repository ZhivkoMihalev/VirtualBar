using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class ListForSaleRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Asking price must be greater than zero.")]
    public decimal AskingPrice { get; set; }

    [Required]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// How many bottles out of the entry's <c>Quantity</c> go on sale. Validated against the owned
    /// count in the decorator. Omitting the field keeps the default of 1, so older clients list a
    /// single bottle instead of the whole stack.
    /// </summary>
    public int Quantity { get; set; } = 1;
}
