using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Badges;

public sealed class BadgeProgressDto
{
    public BadgeType Badge { get; set; }

    public int Threshold { get; set; }

    public int Current { get; set; }

    public bool Earned { get; set; }

    public DateTime? AwardedAt { get; set; }
}
