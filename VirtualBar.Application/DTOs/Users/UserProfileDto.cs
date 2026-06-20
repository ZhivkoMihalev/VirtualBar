namespace VirtualBar.Application.DTOs.Users;

public sealed class UserProfileDto
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public int BottleCount { get; set; }

    public int FollowerCount { get; set; }

    public int FollowingCount { get; set; }

    public bool IsFollowedByMe { get; set; }
}
