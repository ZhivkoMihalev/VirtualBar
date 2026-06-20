using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;

namespace VirtualBar.Application.Interfaces;

public interface IProductLookupService
{
    Task<Result<BarcodeProductDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken);
}
