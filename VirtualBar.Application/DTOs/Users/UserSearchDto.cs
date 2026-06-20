namespace VirtualBar.Application.DTOs.Users;

public sealed class UserSearchDto
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string? Country { get; set; }

    public int BottleCount { get; set; }

    public int FollowerCount { get; set; }
}
