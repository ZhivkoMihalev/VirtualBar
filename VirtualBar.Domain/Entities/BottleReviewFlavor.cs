using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public class BottleReviewFlavor
{
    public Guid ReviewId { get; set; }

    public FlavorTag Flavor { get; set; }

    public BottleReview Review { get; set; } = null!;
}
