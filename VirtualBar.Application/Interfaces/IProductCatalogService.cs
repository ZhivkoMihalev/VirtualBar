using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Interfaces;

public interface IProductCatalogService
{
    Task<Result<List<ProductDto>>> SearchAsync(string search, SpiritCategory? category, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the public catalog. <paramref name="search"/> is optional: a term filters like
    /// <see cref="SearchAsync"/>. <paramref name="sort"/> orders the page; null means <see cref="ProductSort.Name"/>
    /// without a term and <see cref="ProductSort.Relevance"/> with one.
    /// </summary>
    Task<Result<PagedResult<ProductCardDto>>> BrowseAsync(string? search, SpiritCategory? category, ProductSort? sort, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// The product page: the product plus the community score, top flavors, owners, listings and the newest
    /// reviews across every non-deleted bottle linked to it. <c>NotFound</c> for an unknown or deleted product.
    /// </summary>
    Task<Result<ProductDetailDto>> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the reviews of every non-deleted bottle linked to the product, newest first.
    /// <c>NotFound</c> for an unknown or deleted product.
    /// </summary>
    Task<Result<PagedResult<BottleReviewDto>>> GetProductReviewsAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken);
}
