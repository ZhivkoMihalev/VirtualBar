namespace VirtualBar.Domain.Entities;

public sealed class NewsPost : BaseEntity
{
    public string? CoverImageUrl { get; set; }

    public Guid AuthorId { get; set; }

    public AppUser Author { get; set; } = null!;

    public ICollection<NewsPostTranslation> Translations { get; set; } = [];
}
