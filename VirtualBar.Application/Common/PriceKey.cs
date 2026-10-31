using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Common;

/// <summary>
/// Decides which <c>PriceSnapshot</c> row prices a bottle. Every pricing path must go through this
/// helper: if two paths computed the key differently, one would write snapshots the other never reads.
/// </summary>
public static class PriceKey
{
    /// <summary>
    /// Returns the price key of a bottle.
    /// </summary>
    /// <param name="productCanonicalKey">
    /// The stored <c>Product.CanonicalKey</c> when the bottle is linked to a non-deleted catalog product,
    /// otherwise <c>null</c>. A linked bottle is priced as its product, so every copy shares one snapshot
    /// whatever its spelling or vintage.
    /// </param>
    /// <param name="distilleryName">The bottle's distillery name (used only when not linked).</param>
    /// <param name="name">The bottle's name (used only when not linked).</param>
    /// <param name="category">The bottle's category (used only when not linked).</param>
    /// <param name="age">The bottle's age statement (used only when not linked).</param>
    /// <param name="vintageYear">The bottle's vintage (used only when not linked).</param>
    /// <param name="volumeMl">The bottle's volume (used only when not linked).</param>
    public static string ForBottle(
        string? productCanonicalKey,
        string? distilleryName,
        string name,
        SpiritCategory category,
        int? age,
        int? vintageYear,
        int? volumeMl)
        => productCanonicalKey ?? ProductKey.For(distilleryName, name, category, age, vintageYear, volumeMl);
}
