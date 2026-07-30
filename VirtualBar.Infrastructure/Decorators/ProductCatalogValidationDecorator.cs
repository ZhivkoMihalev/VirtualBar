using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class ProductCatalogValidationDecorator(IProductCatalogService inner) : IProductCatalogService
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
}
