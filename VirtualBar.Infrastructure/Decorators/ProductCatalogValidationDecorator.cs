using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class ProductCatalogValidationDecorator(IProductCatalogService inner, AppDbContext db) : IProductCatalogService
{
    private const int DefaultLimit = 20;

    /// <summary>How many results a client is expected to actually render.</summary>
    private const int DisplayLimit = 50;

    /// <summary>
    /// One over <see cref="DisplayLimit"/>. A client that asks for this can tell "exactly the display
    /// limit" apart from "more than that" purely by whether the extra row came back — no count query and
    /// no second round trip — so it never tells the user to refine a search that has nothing left to show.
    /// </summary>
    private const int MaxLimit = DisplayLimit + 1;

    public async Task<Result<List<ProductDto>>> SearchAsync(string search, SpiritCategory? category, int limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var term = search?.Trim() ?? string.Empty;

        if (term.Length < 2)
            return Result<List<ProductDto>>.Fail("Search term must be at least 2 characters.");

        if (term.Length > 100)
            return Result<List<ProductDto>>.Fail("Search term is too long.");

        if (limit < 1) limit = DefaultLimit;
        if (limit > MaxLimit) limit = MaxLimit;

        return await inner.SearchAsync(term, category, limit, cancellationToken);
    }

    public async Task<Result<PagedResult<ProductCardDto>>> BrowseAsync(string? search, SpiritCategory? category, int page, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Browsing without a term is the normal case, so a blank term means "no filter", not an error.
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (term is not null && term.Length < 2)
            return Result<PagedResult<ProductCardDto>>.Fail("Search term must be at least 2 characters.");

        if (term is not null && term.Length > 100)
            return Result<PagedResult<ProductCardDto>>.Fail("Search term is too long.");

        if (page < 1) page = 1;

        // A deep page is an expensive OFFSET and never a real reader; refuse it rather than run it.
        if (page > MaxPage)
            return Result<PagedResult<ProductCardDto>>.Fail("Page is out of range.");

        if (pageSize < 1) pageSize = DefaultBrowsePageSize;
        if (pageSize > MaxBrowsePageSize) pageSize = MaxBrowsePageSize;

        return await inner.BrowseAsync(term, category, page, pageSize, cancellationToken);
    }

    public async Task<Result<ProductDetailDto>> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await ProductExistsAsync(productId, cancellationToken))
            return Result<ProductDetailDto>.NotFound("Product not found.");

        return await inner.GetProductAsync(productId, cancellationToken);
    }

    public async Task<Result<PagedResult<BottleReviewDto>>> GetProductReviewsAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (page < 1) page = 1;

        if (page > MaxPage)
            return Result<PagedResult<BottleReviewDto>>.Fail("Page is out of range.");

        if (pageSize < 1) pageSize = DefaultReviewsPageSize;
        if (pageSize > MaxReviewsPageSize) pageSize = MaxReviewsPageSize;

        if (!await ProductExistsAsync(productId, cancellationToken))
            return Result<PagedResult<BottleReviewDto>>.NotFound("Product not found.");

        return await inner.GetProductReviewsAsync(productId, page, pageSize, cancellationToken);
    }

    private const int MaxPage = 1000;

    private const int DefaultBrowsePageSize = 24;

    private const int MaxBrowsePageSize = 48;

    private const int DefaultReviewsPageSize = 10;

    private const int MaxReviewsPageSize = 50;

    private Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);
}
