using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Auth;

public sealed class ConfirmEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;
}
