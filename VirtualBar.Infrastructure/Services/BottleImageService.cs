using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Storage;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleImageService(
    AppDbContext db,
    IWebHostEnvironment env) : IBottleImageService
{
    public async Task<Result<BottleImageDto>> AddImageAsync(Guid bottleId, IFormFile file, CancellationToken cancellationToken)
    {
        // Extension is derived from the server-validated content type (never the client file name) to
        // prevent persisting an executable/markup extension under wwwroot (stored XSS). Validated in the decorator.
        ImageUploadTypes.TryGetExtension(file.ContentType, out var ext);
        var fileName = $"{Guid.NewGuid()}{ext}";

        var uploadsDir = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
            "uploads", "bottles");
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, fileName);
        await using (var stream = File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var isPrimary = !await db.BottleImages.AnyAsync(i => i.BottleId == bottleId && !i.IsDeleted, cancellationToken);
        var sortOrder = await db.BottleImages.CountAsync(i => i.BottleId == bottleId && !i.IsDeleted, cancellationToken);

        var image = new BottleImage
        {
            BottleId = bottleId,
            Url = $"/uploads/bottles/{fileName}",
            IsPrimary = isPrimary,
            SortOrder = sortOrder
        };

        db.BottleImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);

        return Result<BottleImageDto>.Ok(MapToDto(image));
    }

    public async Task<Result<bool>> DeleteImageAsync(Guid bottleId, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await db.BottleImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.BottleId == bottleId && !i.IsDeleted, cancellationToken);

        image!.IsDeleted = true;
        image.DeletedAt = DateTime.UtcNow;

        // The file goes only when it is an upload inside the bottle folder and nothing else shows it: a bottle linked
        // from the catalog, or the product itself, can show the very same upload (#134).
        var filePath = BottleUploads.FilePathOf(env, image.Url);
        if (filePath is not null && !await IsShownElsewhereAsync(image, cancellationToken))
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Ignore missing/locked file; soft-delete in DB is authoritative.
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// Whether another live image or any catalog product, deleted or not, has the same URL. The image itself is
    /// excluded by id: its soft delete is not saved yet.
    /// </summary>
    private async Task<bool> IsShownElsewhereAsync(BottleImage image, CancellationToken cancellationToken) =>
        await db.BottleImages.AnyAsync(i => i.Id != image.Id && i.Url == image.Url && !i.IsDeleted, cancellationToken)
        || await db.Products.AnyAsync(p => p.ImageUrl == image.Url, cancellationToken);

    public async Task<Result<BottleImageDto>> LinkImageAsync(Guid bottleId, LinkImageRequest request, CancellationToken cancellationToken)
    {
        // The image is always the product's own (#131); the decorator has checked that the product is live and has one.
        var imageUrl = await db.Products
            .Where(p => p.Id == request.ProductId)
            .Select(p => p.ImageUrl!)
            .FirstAsync(cancellationToken);

        var isPrimary = !await db.BottleImages.AnyAsync(i => i.BottleId == bottleId && !i.IsDeleted, cancellationToken);
        var sortOrder = await db.BottleImages.CountAsync(i => i.BottleId == bottleId && !i.IsDeleted, cancellationToken);

        var image = new BottleImage
        {
            BottleId = bottleId,
            Url = imageUrl,
            IsPrimary = isPrimary,
            SortOrder = sortOrder
        };

        db.BottleImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);

        return Result<BottleImageDto>.Ok(MapToDto(image));
    }

    private static BottleImageDto MapToDto(BottleImage image) => new()
    {
        Id = image.Id,
        Url = image.Url,
        IsPrimary = image.IsPrimary,
        SortOrder = image.SortOrder
    };
}
