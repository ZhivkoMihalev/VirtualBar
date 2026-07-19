using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Badges;

public sealed class UserBadgeDto
{
    public BadgeType Badge { get; set; }

    public DateTime AwardedAt { get; set; }
}
