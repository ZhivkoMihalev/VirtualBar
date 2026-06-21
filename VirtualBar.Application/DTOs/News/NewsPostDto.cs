namespace VirtualBar.Application.DTOs.News;

public sealed class NewsPostDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }

    public Guid AuthorId { get; set; }

    public string AuthorDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
