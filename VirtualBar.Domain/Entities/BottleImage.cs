namespace VirtualBar.Domain.Entities;

public class BottleImage : BaseEntity
{
    public Guid BottleId { get; set; }
    public Bottle Bottle { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}
