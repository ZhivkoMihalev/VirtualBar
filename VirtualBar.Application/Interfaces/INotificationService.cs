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

    /// <summary>
    /// Creates a system notification about the recipient's own milestone (e.g. an earned badge). Unlike
    /// <c>CreateAsync</c>, the recipient MAY equal the current user — there is no self-skip — and the
    /// actor recorded on the notification is the recipient themselves.
    /// </summary>
    Task CreateSystemAsync(Guid recipientId, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken);
}
