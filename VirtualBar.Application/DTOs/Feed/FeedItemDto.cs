using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Feed;

public sealed class FeedItemDto
{
    public FeedItemType Type { get; set; }

    public DateTime Timestamp { get; set; }

    public Guid? PostId { get; set; }

    public string? PostTitle { get; set; }

    public string? PostContent { get; set; }

    public string? PostCoverImageUrl { get; set; }

    public string? PostAuthorDisplayName { get; set; }

    public Guid? BottleId { get; set; }

    public string? BottleName { get; set; }

    public string? BottleCategory { get; set; }

    public string? BottlePrimaryImageUrl { get; set; }

    public Guid? BottleUserId { get; set; }

    public string? BottleUserDisplayName { get; set; }

    public decimal? AskingPrice { get; set; }

    public string? Currency { get; set; }
}
