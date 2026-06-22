namespace VirtualBar.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public Guid ActorId { get; set; }

    public string ActorDisplayName { get; set; } = string.Empty;

    public Guid? ResourceId { get; set; }

    public string? ResourceName { get; set; }

    public bool IsRead { get; set; }

    public AppUser User { get; set; } = null!;

    public AppUser Actor { get; set; } = null!;
}
