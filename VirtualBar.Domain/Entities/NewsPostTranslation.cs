namespace VirtualBar.Domain.Entities;

public sealed class NewsPostTranslation
{
    public Guid PostId { get; set; }

    public NewsPost Post { get; set; } = null!;

    public string LanguageCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
