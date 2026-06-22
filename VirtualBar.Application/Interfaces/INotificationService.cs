using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Notifications;
using VirtualBar.Domain.Entities;

namespace VirtualBar.Application.Interfaces;

public interface INotificationService
{
    Task<Result<NotificationSummaryDto>> GetNotificationsAsync(CancellationToken cancellationToken);

    Task<Result<bool>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task<Result<bool>> MarkAllReadAsync(CancellationToken cancellationToken);

    Task CreateAsync(Guid recipientId, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken);

    Task CreateBulkAsync(IEnumerable<Guid> recipientIds, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken);
}
