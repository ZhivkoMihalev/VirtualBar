using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Interfaces;

public interface IProductCatalogService
{
    Task<Result<List<ProductDto>>> SearchAsync(string search, SpiritCategory? category, int limit, CancellationToken cancellationToken);
}
