namespace VirtualBar.Application.DTOs.News;

public sealed class CreateNewsPostRequest
{
    public string Title { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }
}
