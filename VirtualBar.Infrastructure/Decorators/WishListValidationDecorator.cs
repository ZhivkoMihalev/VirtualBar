using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.WishList;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class WishListValidationDecorator(
    IWishListService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IWishListService
{
    public async Task<Result<List<WishListItemDto>>> GetWishListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetWishListAsync(cancellationToken);
    }

    public async Task<Result<List<PublicWishListItemDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetAllAsync(cancellationToken);
    }

    public async Task<Result<WishListItemDto>> AddItemAsync(AddWishListItemRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.DistilleryId is null && request.Category is null)
            return Result<WishListItemDto>.Fail("At least one matching criterion (distillery or category) is required.");

        if (request.DistilleryId is Guid distilleryId)
        {
            var distilleryExists = await db.Distilleries
                .AnyAsync(d => d.Id == distilleryId && !d.IsDeleted, cancellationToken);
            if (!distilleryExists)
                return Result<WishListItemDto>.NotFound("Distillery not found.");
        }

        return await inner.AddItemAsync(request, cancellationToken);
    }

    public async Task<Result<string>> UploadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (file == null || file.Length == 0)
            return Result<string>.Fail("File is required.");

        var allowedTypes = new HashSet<string> { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return Result<string>.Fail("Only JPEG, PNG and WebP images are allowed.");

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
            return Result<string>.Fail("File size must not exceed 5 MB.");

        return await inner.UploadImageAsync(file, cancellationToken);
    }

    public async Task<Result<bool>> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var item = await db.WishListItems.FirstOrDefaultAsync(w => w.Id == itemId && !w.IsDeleted, cancellationToken);

        if (item is null)
            return Result<bool>.NotFound("Wish list item not found.");

        if (item.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        return await inner.RemoveItemAsync(itemId, cancellationToken);
    }
}
