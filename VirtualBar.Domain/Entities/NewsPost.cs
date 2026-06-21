namespace VirtualBar.Domain.Entities;

public sealed class NewsPost : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }

    public Guid AuthorId { get; set; }

    public AppUser Author { get; set; } = null!;
}
