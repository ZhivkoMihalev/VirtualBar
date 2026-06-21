namespace VirtualBar.Application.DTOs.News;

public sealed class CreateNewsPostRequest
{
    public string? CoverImageUrl { get; set; }

    public List<NewsPostTranslationRequest> Translations { get; set; } = [];
}
