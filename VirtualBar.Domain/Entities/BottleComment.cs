namespace VirtualBar.Domain.Entities;

public class BottleComment : BaseEntity
{
    public Guid BottleId { get; set; }
    public Bottle Bottle { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
}
