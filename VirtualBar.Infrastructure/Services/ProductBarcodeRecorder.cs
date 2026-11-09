using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

/// <summary>
/// Records the barcode of a bottle linked to a catalog product as a barcode of that product (#49), wherever a bottle
/// gets linked: adding or updating it (<see cref="BottleService"/>) and an admin approving its product request
/// (<see cref="ProductRequestService"/>). The record is only added to the context, so it is saved with the bottle.
/// </summary>
internal static class ProductBarcodeRecorder
{
    /// <summary>
    /// Adds the pair unless it is known in any form of the barcode: already recorded, waiting in this same save, or
    /// removed by an admin, which must stay removed.
    /// </summary>
    public static async Task RecordAsync(
        AppDbContext db, Guid productId, string barcode, Guid collectorId, CancellationToken cancellationToken)
    {
        string[] codes = [.. BarcodeFormat.Equivalents(barcode)];

        var known = db.ProductBarcodes.Local.Any(b => b.ProductId == productId && codes.Contains(b.Barcode))
            || await db.ProductBarcodes.AnyAsync(b => b.ProductId == productId && codes.Contains(b.Barcode), cancellationToken);

        if (known)
            return;

        db.ProductBarcodes.Add(new ProductBarcode { ProductId = productId, Barcode = barcode, AddedByUserId = collectorId });
    }
}
