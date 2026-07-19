using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Badges;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Common;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BadgeService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService,
    ILogger<BadgeService> logger) : IBadgeService
{
    public async Task EvaluateAsync(Guid userId, BadgeTrigger trigger, CancellationToken cancellationToken)
    {
        try
        {
            var defs = BadgeCatalog.ForTrigger(trigger);

            var earned = await db.UserBadges
                .Where(b => b.UserId == userId)
                .Select(b => b.Badge)
                .ToListAsync(cancellationToken);

            var missing = defs.Where(d => !earned.Contains(d.Type)).ToList();
            if (missing.Count == 0)
                return;

            var counts = new Dictionary<BadgeCountKind, int>();
            foreach (var countKind in missing.Select(d => d.CountKind).Distinct())
                counts[countKind] = await CountAsync(countKind, userId, cancellationToken);

            foreach (var def in missing)
            {
                if (counts[def.CountKind] < def.Threshold)
                    continue;

                var badge = new UserBadge
                {
                    UserId = userId,
                    Badge = def.Type,
                    AwardedAt = DateTime.UtcNow,
                };

                db.UserBadges.Add(badge);

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    db.Entry(badge).State = EntityState.Detached;
                    continue;
                }

                await notificationService.CreateSystemAsync(
                    userId, NotificationType.BadgeEarned, null, def.Type.ToString(), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Badge evaluation failed for user {UserId} on trigger {Trigger}.", userId, trigger);
        }
    }

    public async Task<Result<List<UserBadgeDto>>> GetUserBadgesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var badges = await db.UserBadges
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.AwardedAt)
            .Select(b => new UserBadgeDto
            {
                Badge = b.Badge,
                AwardedAt = b.AwardedAt,
            })
            .ToListAsync(cancellationToken);

        return Result<List<UserBadgeDto>>.Ok(badges);
    }

    public async Task<Result<List<BadgeProgressDto>>> GetMyProgressAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        var earned = await db.UserBadges
            .Where(b => b.UserId == userId)
            .ToDictionaryAsync(b => b.Badge, b => b.AwardedAt, cancellationToken);

        var counts = new Dictionary<BadgeCountKind, int>();
        foreach (var countKind in BadgeCatalog.All.Select(d => d.CountKind).Distinct())
            counts[countKind] = await CountAsync(countKind, userId, cancellationToken);

        var progress = BadgeCatalog.All
            .Select(def =>
            {
                var isEarned = earned.TryGetValue(def.Type, out var awardedAt);

                return new BadgeProgressDto
                {
                    Badge = def.Type,
                    Threshold = def.Threshold,
                    Current = counts[def.CountKind],
                    Earned = isEarned,
                    AwardedAt = isEarned ? awardedAt : null,
                };
            })
            .ToList();

        return Result<List<BadgeProgressDto>>.Ok(progress);
    }

    private Task<int> CountAsync(BadgeCountKind countKind, Guid userId, CancellationToken cancellationToken) =>
        countKind switch
        {
            BadgeCountKind.Bottles =>
                db.Bottles.CountAsync(b => b.UserId == userId && !b.IsDeleted, cancellationToken),
            BadgeCountKind.Categories =>
                db.Bottles.Where(b => b.UserId == userId && !b.IsDeleted)
                    .Select(b => b.Category)
                    .Distinct()
                    .CountAsync(cancellationToken),
            BadgeCountKind.LimitedBottles =>
                db.Bottles.CountAsync(b => b.UserId == userId && !b.IsDeleted && b.IsLimited, cancellationToken),
            BadgeCountKind.LikesReceived =>
                db.BottleLikes.CountAsync(l => l.Bottle.UserId == userId && !l.Bottle.IsDeleted, cancellationToken),
            BadgeCountKind.Followers =>
                db.UserFollows.CountAsync(f => f.FollowedId == userId, cancellationToken),
            BadgeCountKind.ActiveListings =>
                db.Bottles.CountAsync(b => b.UserId == userId && !b.IsDeleted && b.IsForSale, cancellationToken),
            BadgeCountKind.SalesAccepted =>
                db.Offers.CountAsync(o => o.SellerId == userId && o.Status == OfferStatus.Accepted && !o.IsDeleted, cancellationToken),
            BadgeCountKind.PurchasesAccepted =>
                db.Offers.CountAsync(o => o.BuyerId == userId && o.Status == OfferStatus.Accepted && !o.IsDeleted, cancellationToken),
            _ => Task.FromResult(0),
        };
}
