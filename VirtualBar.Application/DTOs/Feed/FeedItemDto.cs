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

    /// <summary>The bottle's owner. For a <see cref="FeedItemType.Reviewed"/> item the reviewer is the actor instead.</summary>
    public Guid? BottleUserId { get; set; }

    public string? BottleUserDisplayName { get; set; }

    public decimal? AskingPrice { get; set; }

    public string? Currency { get; set; }

    /// <summary>
    /// Who did it, when that need not be the bottle's owner: the reviewer of a <see cref="FeedItemType.Reviewed"/> item.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    public string? ActorDisplayName { get; set; }

    public string? ActorAvatarUrl { get; set; }

    public Guid? ReviewId { get; set; }

    public int? ReviewScore { get; set; }

    /// <summary>The start of the review's summary, cut at a word boundary with an ellipsis when it is long.</summary>
    public string? ReviewSummary { get; set; }

    /// <summary>The review's first flavors in enum order, at most three.</summary>
    public List<FlavorTag>? ReviewFlavors { get; set; }

    /// <summary>The catalog product the bottle is linked to, when that product is not deleted.</summary>
    public Guid? ProductId { get; set; }

    public string? ProductName { get; set; }

    /// <summary>The product's brand, falling back to its non-deleted distillery's name, as the catalog shows it.</summary>
    public string? ProductBrand { get; set; }
}
