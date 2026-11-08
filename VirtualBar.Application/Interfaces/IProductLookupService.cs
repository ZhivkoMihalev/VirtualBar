using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;

namespace VirtualBar.Application.Interfaces;

public interface IProductLookupService
{
    Task<Result<ProductDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken);
}
