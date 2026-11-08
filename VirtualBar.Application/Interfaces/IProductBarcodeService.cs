using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductBarcodes;

namespace VirtualBar.Application.Interfaces;

/// <summary>The admin's view of the barcodes collectors gave catalog products (#49).</summary>
public interface IProductBarcodeService
{
    /// <summary>The live pairs, newest first, optionally those whose barcode or product matches <paramref name="search"/>.</summary>
    Task<Result<PagedResult<ProductBarcodeDto>>> GetAllAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Removes a wrong pair, in every form of its barcode, so the lookup and new saves never give it again.</summary>
    Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken);
}
