namespace VirtualBar.Domain.Enums;

/// <summary>
/// The order of the public catalog (<c>GET /api/products/browse?sort=</c>). Every order ends with name, then id, so
/// paging never repeats or skips a product. Sent and read by name, so members are never renamed.
/// </summary>
public enum ProductSort
{
    /// <summary>Name A–Z. The default without a search term.</summary>
    Name,

    /// <summary>Names that start with the search term first, then name. The default with a term.</summary>
    Relevance,

    /// <summary>
    /// Products with at least three reviews first, then those with one or two, then the unreviewed; within each,
    /// average score high to low, then review count.
    /// </summary>
    TopRated,

    /// <summary>Review count, high to low.</summary>
    MostReviewed,

    /// <summary>Distinct owners, high to low.</summary>
    Popular,

    /// <summary>Current listings, high to low.</summary>
    ForSale
}
