using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleImageService(
    AppDbContext db,
    IWebHostEnvironment env) : IBottleImageService
{
    public async Task<Result<BottleImageDto>> AddImageAsync(Guid bottleId, IFormFile file, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
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

    public async Task<Result<bool>> DeleteImageAsync(Guid imageId, CancellationToken cancellationToken)
    {
        var image = await db.BottleImages
            .FirstOrDefaultAsync(i => i.Id == imageId && !i.IsDeleted, cancellationToken);

        image!.IsDeleted = true;
        image.DeletedAt = DateTime.UtcNow;

        var relativePath = image.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var rootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var filePath = Path.Combine(rootPath, relativePath);

        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Ignore missing/locked file; soft-delete in DB is authoritative.
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<BottleImageDto>> LinkImageAsync(Guid bottleId, LinkImageRequest request, CancellationToken cancellationToken)
    {
        var isPrimary = !await db.BottleImages.AnyAsync(i => i.BottleId == bottleId && !i.IsDeleted, cancellationToken);
        var sortOrder = await db.BottleImages.CountAsync(i => i.BottleId == bottleId && !i.IsDeleted, cancellationToken);

        var image = new BottleImage
        {
            BottleId = bottleId,
            Url = request.Url,
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
