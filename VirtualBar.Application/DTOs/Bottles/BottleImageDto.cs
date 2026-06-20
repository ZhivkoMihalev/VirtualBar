namespace VirtualBar.Application.DTOs.Bottles;

public sealed class BottleImageDto
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }
}
