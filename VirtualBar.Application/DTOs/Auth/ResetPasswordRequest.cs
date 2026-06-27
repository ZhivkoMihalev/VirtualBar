using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Auth;

public sealed class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(
        @"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter and one digit.")]
    public string NewPassword { get; init; } = string.Empty;
}
