using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Notifications;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class NotificationService(
    AppDbContext db,
    ICurrentUser currentUser) : INotificationService
{
    public async Task<Result<NotificationSummaryDto>> GetNotificationsAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        var notifications = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                ActorId = n.ActorId,
                ActorDisplayName = n.ActorDisplayName,
                ResourceId = n.ResourceId,
                ResourceName = n.ResourceName,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var unreadCount = await db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsDeleted && !n.IsRead, cancellationToken);

        return Result<NotificationSummaryDto>.Ok(new NotificationSummaryDto
        {
            Notifications = notifications,
            UnreadCount = unreadCount,
        });
    }

    public async Task<Result<bool>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && !n.IsDeleted, cancellationToken);

        notification!.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        await db.Notifications
            .Where(n => n.UserId == currentUser.UserId && !n.IsDeleted && !n.IsRead)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.IsRead, true)
                      .SetProperty(n => n.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task CreateAsync(Guid recipientId, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken)
    {
        var actorDisplayName = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        if (actorDisplayName is null) return;

        db.Notifications.Add(new Notification
        {
            UserId = recipientId,
            Type = type,
            ActorId = currentUser.UserId,
            ActorDisplayName = actorDisplayName,
            ResourceId = resourceId,
            ResourceName = resourceName,
            IsRead = false,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateBulkAsync(IEnumerable<Guid> recipientIds, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken)
    {
        var ids = recipientIds.ToList();
        if (ids.Count == 0) return;

        var actorDisplayName = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        if (actorDisplayName is null) return;

        db.Notifications.AddRange(ids.Select(recipientId => new Notification
        {
            UserId = recipientId,
            Type = type,
            ActorId = currentUser.UserId,
            ActorDisplayName = actorDisplayName,
            ResourceId = resourceId,
            ResourceName = resourceName,
            IsRead = false,
        }));

        await db.SaveChangesAsync(cancellationToken);
    }
}
