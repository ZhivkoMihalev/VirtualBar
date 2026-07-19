using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Badges;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Interfaces;

public interface IBadgeService
{
    /// <summary>
    /// Fire-and-forget re-evaluation of the user's badges for the given trigger: awards every catalog
    /// entry whose count now meets its threshold. Called inline from trigger services — returns a plain
    /// <c>Task</c> (not <c>Result</c>), never throws, and never breaks the host operation.
    /// </summary>
    Task EvaluateAsync(Guid userId, BadgeTrigger trigger, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the badges the user has already earned, newest first. Feeds the public
    /// <c>BadgesController</c> read endpoint.
    /// </summary>
    Task<Result<List<UserBadgeDto>>> GetUserBadgesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the full badge catalog projected for the current user — threshold, live count, earned
    /// state and award date per entry. Own-only; feeds the <c>BadgesController</c> progress endpoint.
    /// </summary>
    Task<Result<List<BadgeProgressDto>>> GetMyProgressAsync(CancellationToken cancellationToken);
}
