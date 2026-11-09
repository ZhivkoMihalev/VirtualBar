namespace VirtualBar.Domain.Enums;

/// <summary>
/// Where an offer stands. Append-only: the values are stored as ints, so never reorder or rename a member.
/// </summary>
public enum OfferStatus
{
    Pending,

    /// <summary>A completed deal: the seller accepted and the buyer confirmed they received the bottle (#130).</summary>
    Accepted,

    Declined,

    Withdrawn,

    /// <summary>The seller accepted; one bottle is reserved for the buyer until they confirm receipt (#130).</summary>
    AwaitingReceipt,

    /// <summary>A deal one side ended before the buyer confirmed receipt; the reservation is released (#130).</summary>
    Cancelled,
}
