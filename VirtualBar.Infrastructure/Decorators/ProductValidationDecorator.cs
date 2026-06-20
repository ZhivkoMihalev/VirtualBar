using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class ProductValidationDecorator(
    ProductLookupService inner) : IProductLookupService
{
    public async Task<Result<BarcodeProductDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(barcode))
            return Result<BarcodeProductDto>.Fail("Barcode is required.");

        try
        {
            return await inner.LookupByBarcodeAsync(barcode, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Result<BarcodeProductDto>.Fail("Barcode lookup failed. Please try again.");
        }
    }
}
