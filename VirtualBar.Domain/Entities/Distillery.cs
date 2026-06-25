namespace VirtualBar.Domain.Entities;

public sealed class Distillery : BaseEntity
{
    public string Name { get; set; } = "";

    public string? Country { get; set; }

    public string? Region { get; set; }

    public ICollection<Bottle> Bottles { get; set; } = [];

    public ICollection<DistilleryCategory> Categories { get; set; } = [];
}
