namespace VirtualBar.Domain.Entities;

/// <summary>
/// A barcode a collector gave a catalog product (#49): the collector saved a bottle with this barcode linked to this
/// product. One product can have several (with or without a gift box, other markets, a new label), and the record
/// stays when the bottle is deleted, so what collectors scanned builds the platform's own barcode database. The
/// product's own <see cref="Product.Barcode"/> comes first in a lookup.
/// </summary>
public sealed class ProductBarcode : BaseEntity
{
    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>8 to 14 digits, trimmed.</summary>
    public string Barcode { get; set; } = string.Empty;

    /// <summary>The collector who first linked this barcode to this product.</summary>
    public Guid AddedByUserId { get; set; }

    public AppUser AddedByUser { get; set; } = null!;
}
