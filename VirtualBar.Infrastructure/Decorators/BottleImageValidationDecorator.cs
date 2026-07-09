using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;
using VirtualBar.Infrastructure.Storage;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class BottleImageValidationDecorator(
    BottleImageService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IBottleImageService
{
    public async Task<Result<BottleImageDto>> AddImageAsync(Guid bottleId, IFormFile file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (file is null || file.Length == 0)
            return Result<BottleImageDto>.Fail("No file provided.");

        if (!ImageUploadTypes.IsAllowed(file.ContentType))
            return Result<BottleImageDto>.Fail($"Only {ImageUploadTypes.AllowedFormatsLabel} images are allowed.");

        if (file.Length > 10 * 1024 * 1024)
            return Result<BottleImageDto>.Fail("Image must be under 10 MB.");

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<BottleImageDto>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<BottleImageDto>.Forbidden("Forbidden.");

        return await inner.AddImageAsync(bottleId, file, cancellationToken);
    }

    public async Task<Result<bool>> DeleteImageAsync(Guid imageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var image = await db.BottleImages.FirstOrDefaultAsync(i => i.Id == imageId && !i.IsDeleted, cancellationToken);
        if (image is null)
            return Result<bool>.NotFound("Image not found.");

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == image.BottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<bool>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        return await inner.DeleteImageAsync(imageId, cancellationToken);
    }

    public async Task<Result<BottleImageDto>> LinkImageAsync(Guid bottleId, LinkImageRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Url))
            return Result<BottleImageDto>.Fail("Image URL is required.");

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<BottleImageDto>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<BottleImageDto>.Forbidden("Forbidden.");

        return await inner.LinkImageAsync(bottleId, request, cancellationToken);
    }
}
