using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public class UserBadge
{
    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public BadgeType Badge { get; set; }

    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
}
