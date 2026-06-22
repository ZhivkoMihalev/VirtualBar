using VirtualBar.Domain.Entities;

namespace VirtualBar.Application.DTOs.Notifications;

public sealed class NotificationDto
{
    public Guid Id { get; set; }

    public NotificationType Type { get; set; }

    public Guid ActorId { get; set; }

    public string ActorDisplayName { get; set; } = string.Empty;

    public Guid? ResourceId { get; set; }

    public string? ResourceName { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
