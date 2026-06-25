using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.WishList;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class WishListService(
    AppDbContext db,
    ICurrentUser currentUser,
    IWebHostEnvironment env) : IWishListService
{
    private static readonly Dictionary<string, string> AllowedImageTypes = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

    public async Task<Result<List<WishListItemDto>>> GetWishListAsync(CancellationToken cancellationToken)
    {
        var items = await db.WishListItems
            .Where(w => w.UserId == currentUser.UserId && !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishListItemDto
            {
                Id = w.Id,
                BottleName = w.BottleName,
                Distillery = w.Distillery,
                Category = w.Category,
                ImageUrl = w.ImageUrl,
                CreatedAt = w.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Result<List<WishListItemDto>>.Ok(items);
    }

    public async Task<Result<List<PublicWishListItemDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await db.WishListItems
            .Where(w => !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new PublicWishListItemDto
            {
                Id = w.Id,
                BottleName = w.BottleName,
                Distillery = w.Distillery,
                Category = w.Category,
                ImageUrl = w.ImageUrl,
                UserId = w.UserId,
                UserDisplayName = w.User.DisplayName,
                CreatedAt = w.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Result<List<PublicWishListItemDto>>.Ok(items);
    }

    public async Task<Result<WishListItemDto>> AddItemAsync(AddWishListItemRequest request, CancellationToken cancellationToken)
    {
        var item = new WishListItem
        {
            UserId = currentUser.UserId,
            BottleName = request.BottleName,
            Distillery = request.Distillery,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
        };

        db.WishListItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return Result<WishListItemDto>.Ok(new WishListItemDto
        {
            Id = item.Id,
            BottleName = item.BottleName,
            Distillery = item.Distillery,
            Category = item.Category,
            ImageUrl = item.ImageUrl,
            CreatedAt = item.CreatedAt,
        });
    }

    public async Task<Result<string>> UploadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var ext = AllowedImageTypes[file.ContentType];

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "wishlist");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}.{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return Result<string>.Ok($"/uploads/wishlist/{fileName}");
    }

    public async Task<Result<bool>> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await db.WishListItems
            .FirstOrDefaultAsync(w => w.Id == itemId && !w.IsDeleted, cancellationToken);

        item!.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }
}
