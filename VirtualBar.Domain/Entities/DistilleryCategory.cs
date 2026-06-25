using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public class DistilleryCategory
{
    public Guid DistilleryId { get; set; }

    public SpiritCategory Category { get; set; }

    public Distillery Distillery { get; set; } = null!;
}
