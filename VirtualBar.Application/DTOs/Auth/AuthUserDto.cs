namespace VirtualBar.Application.DTOs.Auth;

public sealed class AuthUserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public DateTime CreatedAt { get; set; }
}
