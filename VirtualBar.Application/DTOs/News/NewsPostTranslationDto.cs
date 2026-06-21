namespace VirtualBar.Application.DTOs.News;

public sealed class NewsPostTranslationDto
{
    public string LanguageCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
