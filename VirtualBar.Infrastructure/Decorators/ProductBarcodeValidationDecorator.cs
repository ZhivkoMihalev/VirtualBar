using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductBarcodes;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class ProductBarcodeValidationDecorator(
    ProductBarcodeService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IProductBarcodeService
{
    private const string AdminsOnly = "Only administrators can manage product barcodes.";

    private const int MaxPage = 1000;

    private const int DefaultPageSize = 30;

    private const int MaxPageSize = 50;

    public async Task<Result<PagedResult<ProductBarcodeDto>>> GetAllAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<PagedResult<ProductBarcodeDto>>.Forbidden(AdminsOnly);

        // A blank term lists every pair; a barcode is searched by any part of it, so even one digit is a term.
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (term is { Length: > 100 })
            return Result<PagedResult<ProductBarcodeDto>>.Fail("Search term is too long.");

        if (page < 1) page = 1;

        if (page > MaxPage)
            return Result<PagedResult<ProductBarcodeDto>>.Fail("Page is out of range.");

        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        return await inner.GetAllAsync(term, page, pageSize, cancellationToken);
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<bool>.Forbidden(AdminsOnly);

        var exists = await db.ProductBarcodes.AnyAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
        if (!exists)
            return Result<bool>.NotFound("Product barcode not found.");

        return await inner.RemoveAsync(id, cancellationToken);
    }
}
