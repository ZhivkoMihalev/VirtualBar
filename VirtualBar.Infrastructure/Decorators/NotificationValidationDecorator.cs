using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Notifications;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class NotificationValidationDecorator(
    INotificationService inner,
    AppDbContext db,
    ICurrentUser currentUser) : INotificationService
{
    public async Task<Result<NotificationSummaryDto>> GetNotificationsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.GetNotificationsAsync(cancellationToken);
    }

    public async Task<Result<bool>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var notification = await db.Notifications.FindAsync([notificationId], cancellationToken);

        if (notification is null || notification.IsDeleted)
            return Result<bool>.NotFound("Notification not found.");

        if (notification.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Access denied.");

        return await inner.MarkReadAsync(notificationId, cancellationToken);
    }

    public async Task<Result<bool>> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.MarkAllReadAsync(cancellationToken);
    }

    public async Task CreateAsync(Guid recipientId, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (recipientId == currentUser.UserId)
            return;

        await inner.CreateAsync(recipientId, type, resourceId, resourceName, cancellationToken);
    }

    public async Task CreateBulkAsync(IEnumerable<Guid> recipientIds, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filtered = recipientIds.Where(id => id != currentUser.UserId).ToList();
        if (filtered.Count == 0) return;

        await inner.CreateBulkAsync(filtered, type, resourceId, resourceName, cancellationToken);
    }

    public async Task CreateSystemAsync(Guid recipientId, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await inner.CreateSystemAsync(recipientId, type, resourceId, resourceName, cancellationToken);
    }
}
