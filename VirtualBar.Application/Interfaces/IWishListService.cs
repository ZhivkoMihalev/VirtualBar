using Microsoft.AspNetCore.Http;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.WishList;

namespace VirtualBar.Application.Interfaces;

public interface IWishListService
{
    Task<Result<List<WishListItemDto>>> GetWishListAsync(CancellationToken cancellationToken);

    Task<Result<List<PublicWishListItemDto>>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<WishListItemDto>> AddItemAsync(AddWishListItemRequest request, CancellationToken cancellationToken);

    Task<Result<string>> UploadImageAsync(IFormFile file, CancellationToken cancellationToken);

    Task<Result<bool>> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken);
}
