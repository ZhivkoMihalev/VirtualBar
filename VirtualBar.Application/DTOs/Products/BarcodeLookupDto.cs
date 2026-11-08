using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Products;

/// <summary>
/// The answer to a barcode (#49): a catalog product, or, when no product has the barcode, a suggestion from other
/// collectors' bottles. Exactly one of <see cref="Product"/> and <see cref="Suggestion"/> is set.
/// </summary>
public sealed class BarcodeLookupDto
{
    public BarcodeSource Source { get; set; }

    /// <summary>The catalog product, in the shape of the catalog search, for <see cref="BarcodeSource.Catalog"/> and
    /// <see cref="BarcodeSource.Collectors"/>.</summary>
    public ProductDto? Product { get; set; }

    /// <summary>The fields of collectors' bottles that are not in the catalog, for <see cref="BarcodeSource.Bottles"/>.</summary>
    public BarcodeSuggestionDto? Suggestion { get; set; }

    /// <summary>
    /// How many collectors back the answer: for <see cref="BarcodeSource.Collectors"/> those who gave the barcode to this
    /// product, for <see cref="BarcodeSource.Bottles"/> those whose bottles carry it; 0 for the catalog's own barcode.
    /// </summary>
    public int Collectors { get; set; }
}
