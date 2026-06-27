using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Auth;

public sealed class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    public string? Language { get; init; }
}
