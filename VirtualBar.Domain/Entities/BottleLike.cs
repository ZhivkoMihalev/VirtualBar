namespace VirtualBar.Domain.Entities;

public class BottleLike
{
    public Guid BottleId { get; set; }
    public Bottle Bottle { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}
