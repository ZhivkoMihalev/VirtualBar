using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Badges;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class BadgeValidationDecorator(
    BadgeService inner,
    AppDbContext db) : IBadgeService
{
    public async Task EvaluateAsync(Guid userId, BadgeTrigger trigger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty || !Enum.IsDefined(trigger))
            return;

        await inner.EvaluateAsync(userId, trigger, cancellationToken);
    }

    public async Task<Result<List<UserBadgeDto>>> GetUserBadgesAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            return Result<List<UserBadgeDto>>.NotFound("User not found.");

        return await inner.GetUserBadgesAsync(userId, cancellationToken);
    }

    public async Task<Result<List<BadgeProgressDto>>> GetMyProgressAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetMyProgressAsync(cancellationToken);
    }
}
