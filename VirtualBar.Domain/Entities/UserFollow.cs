namespace VirtualBar.Domain.Entities;

public class UserFollow
{
    public Guid FollowerId { get; set; }

    public AppUser Follower { get; set; } = null!;

    public Guid FollowedId { get; set; }

    public AppUser Followed { get; set; } = null!;

    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
}
