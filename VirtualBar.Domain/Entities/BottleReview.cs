namespace VirtualBar.Domain.Entities;

public class BottleReview : BaseEntity
{
    public Guid BottleId { get; set; }

    public Bottle Bottle { get; set; } = null!;

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public int Score { get; set; }

    public string? Nose { get; set; }

    public string? Palate { get; set; }

    public string? Finish { get; set; }

    public string? Summary { get; set; }

    public ICollection<BottleReviewFlavor> Flavors { get; set; } = [];
}
