namespace VirtualBar.Application.DTOs.Users;

public sealed class UpdatedProfileDto
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }
}
