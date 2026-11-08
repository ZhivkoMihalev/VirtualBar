using Microsoft.Extensions.Logging;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class ProductValidationDecorator(
    ProductLookupService inner,
    ILogger<ProductValidationDecorator> logger) : IProductLookupService
{
    public async Task<Result<BarcodeLookupDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(barcode))
            return Result<BarcodeLookupDto>.Fail("Barcode is required.");

        if (!BarcodeFormat.IsValid(barcode))
            return Result<BarcodeLookupDto>.Fail("Barcode must be 8 to 14 digits.");

        var normalized = barcode.Trim();

        try
        {
            return await inner.LookupByBarcodeAsync(normalized, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Barcode lookup failed for {Barcode}.", normalized);
            return Result<BarcodeLookupDto>.Fail("Barcode lookup failed. Please try again.");
        }
    }
}
