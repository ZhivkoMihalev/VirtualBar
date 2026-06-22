namespace VirtualBar.Application.DTOs.Notifications;

public sealed class NotificationSummaryDto
{
    public List<NotificationDto> Notifications { get; set; } = [];

    public int UnreadCount { get; set; }
}
