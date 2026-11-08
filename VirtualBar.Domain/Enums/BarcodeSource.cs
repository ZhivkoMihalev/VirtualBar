namespace VirtualBar.Domain.Enums;

/// <summary>Where a barcode lookup found its answer (#49). Append-only: the frontend shows a message per member.</summary>
public enum BarcodeSource
{
    /// <summary>The catalog product's own barcode.</summary>
    Catalog,

    /// <summary>A barcode collectors gave a catalog product by saving their bottle of it.</summary>
    Collectors,

    /// <summary>Collectors' bottles that are not in the catalog: the answer is a suggestion, not a product.</summary>
    Bottles,
}
