using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductRequests;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class ProductRequestService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService,
    IBadgeService badgeService) : IProductRequestService
{
    public async Task<Result<ProductRequestDto>> CreateAsync(CreateProductRequestRequest request, CancellationToken cancellationToken)
    {
        var distillery = request.DistilleryId is Guid distilleryId
            ? await db.Distilleries.FirstOrDefaultAsync(d => d.Id == distilleryId && !d.IsDeleted, cancellationToken)
            : null;

        var name = request.Name.Trim();
        var brand = Trimmed(request.Brand);

        var entity = new ProductRequest
        {
            UserId = currentUser.UserId,
            Name = name,
            Brand = brand,
            DistilleryId = request.DistilleryId,
            Distillery = distillery,
            Category = request.Category,
            Age = request.Age,
            AbvPercent = request.AbvPercent,
            VolumeMl = request.VolumeMl,
            Barcode = Trimmed(request.Barcode),
            Country = Trimmed(request.Country),
            Region = Trimmed(request.Region),
            UserNote = Trimmed(request.UserNote),
            SourceBottleId = request.SourceBottleId,
            Status = ProductRequestStatus.Pending,
            CanonicalKey = ProductKey.For(distillery?.Name ?? brand, name, request.Category, request.Age, null, request.VolumeMl),
        };

        db.ProductRequests.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(entity).State = EntityState.Detached;
            return Result<ProductRequestDto>.Conflict("This product has already been requested.");
        }

        await db.Entry(entity).Reference(r => r.User).LoadAsync(cancellationToken);

        return Result<ProductRequestDto>.Ok(MapToDto(entity));
    }

    public async Task<Result<List<ProductRequestDto>>> GetMineAsync(CancellationToken cancellationToken)
    {
        var requests = await db.ProductRequests
            .AsNoTracking()
            .Where(r => r.UserId == currentUser.UserId && !r.IsDeleted)
            .Include(r => r.User)
            .Include(r => r.Distillery)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<ProductRequestDto>>.Ok(requests.Select(MapToDto).ToList());
    }

    public async Task<Result<bool>> WithdrawAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await db.ProductRequests
            .FirstAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<List<ProductRequestDto>>> GetAllAsync(ProductRequestStatus? status, CancellationToken cancellationToken)
    {
        var query = db.ProductRequests
            .AsNoTracking()
            .Where(r => !r.IsDeleted);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var requests = await query
            .Include(r => r.User)
            .Include(r => r.Distillery)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<ProductRequestDto>>.Ok(requests.Select(MapToDto).ToList());
    }

    public async Task<Result<ProductRequestDto>> ApproveAsync(Guid requestId, ResolveProductRequestRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.ProductRequests
            .Include(r => r.User)
            .Include(r => r.Distillery)
            .FirstAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);

        Product product;

        if (request.ExistingProductId is Guid existingProductId)
        {
            product = await db.Products
                .FirstAsync(p => p.Id == existingProductId && !p.IsDeleted, cancellationToken);
        }
        else
        {
            product = await BuildProductAsync(entity, request, cancellationToken);

            db.Products.Add(product);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.Entry(product).State = EntityState.Detached;
                return Result<ProductRequestDto>.Conflict("A product with the same canonical identity already exists.");
            }
        }

        var respondedAt = DateTime.UtcNow;

        entity.Status = ProductRequestStatus.Approved;
        entity.ResolvedProductId = product.Id;
        entity.AdminNote = Trimmed(request.AdminNote) ?? entity.AdminNote;
        entity.RespondedAt = respondedAt;
        entity.UpdatedAt = respondedAt;

        var sourceBottle = entity.SourceBottleId is Guid sourceBottleId
            ? await db.Bottles.FirstOrDefaultAsync(
                b => b.Id == sourceBottleId && !b.IsDeleted && b.ProductId == null, cancellationToken)
            : null;

        if (sourceBottle is not null)
        {
            sourceBottle.ProductId = product.Id;
            sourceBottle.UpdatedAt = respondedAt;
        }

        var candidates = await db.Bottles
            .Include(b => b.Distillery)
            .Where(b => !b.IsDeleted
                && b.ProductId == null
                && b.Category == product.Category
                && b.Age == product.Age
                && b.VolumeMl == product.VolumeMl)
            .ToListAsync(cancellationToken);

        foreach (var bottle in candidates)
        {
            // A soft-deleted distillery counts as absent in every other place that builds a key —
            // BottleService.AddBottleAsync and BuildProductAsync above both filter on !IsDeleted. Taking
            // the name unconditionally here would give the candidate a producer segment the product's own
            // key never had, so every bottle whose distillery was since retired would silently fail to
            // retro-link.
            var distilleryName = bottle.Distillery is { IsDeleted: false } ? bottle.Distillery.Name : null;

            var bottleKey = ProductKey.For(distilleryName, bottle.Name, bottle.Category, bottle.Age, null, bottle.VolumeMl);

            if (bottleKey != product.CanonicalKey)
                continue;

            bottle.ProductId = product.Id;
            bottle.UpdatedAt = respondedAt;
        }

        await db.SaveChangesAsync(cancellationToken);

        await notificationService.CreateAsync(
            entity.UserId, NotificationType.ProductRequestApproved, product.Id, product.Name, cancellationToken);

        await badgeService.EvaluateAsync(entity.UserId, BadgeTrigger.ProductRequestApproved, cancellationToken);

        return Result<ProductRequestDto>.Ok(MapToDto(entity));
    }

    public async Task<Result<ProductRequestDto>> RejectAsync(Guid requestId, RejectProductRequestRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.ProductRequests
            .Include(r => r.User)
            .Include(r => r.Distillery)
            .FirstAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);

        var respondedAt = DateTime.UtcNow;

        entity.Status = ProductRequestStatus.Rejected;
        entity.AdminNote = Trimmed(request.AdminNote) ?? entity.AdminNote;
        entity.RespondedAt = respondedAt;
        entity.UpdatedAt = respondedAt;

        await db.SaveChangesAsync(cancellationToken);

        await notificationService.CreateAsync(
            entity.UserId, NotificationType.ProductRequestRejected, entity.Id, entity.Name, cancellationToken);

        return Result<ProductRequestDto>.Ok(MapToDto(entity));
    }

    private async Task<Product> BuildProductAsync(ProductRequest entity, ResolveProductRequestRequest request, CancellationToken cancellationToken)
    {
        var name = Trimmed(request.Name) ?? entity.Name;
        var brand = Trimmed(request.Brand) ?? entity.Brand;
        var distilleryId = request.DistilleryId ?? entity.DistilleryId;
        var category = request.Category ?? entity.Category;
        var age = request.Age ?? entity.Age;
        var volumeMl = request.VolumeMl ?? entity.VolumeMl;

        var distilleryName = distilleryId is Guid id
            ? await db.Distilleries
                .Where(d => d.Id == id && !d.IsDeleted)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new Product
        {
            Name = name,
            Brand = brand,
            DistilleryId = distilleryId,
            Category = category,
            Country = Trimmed(request.Country) ?? entity.Country,
            Region = Trimmed(request.Region) ?? entity.Region,
            Age = age,
            AbvPercent = request.AbvPercent ?? entity.AbvPercent,
            VolumeMl = volumeMl,
            Barcode = Trimmed(request.Barcode) ?? entity.Barcode,
            ImageUrl = await ResolveImageUrlAsync(entity, request, cancellationToken),
            Description = Trimmed(request.Description),
            CanonicalKey = ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl),
            Origin = ProductOrigin.Approved,
        };
    }

    private async Task<string?> ResolveImageUrlAsync(ProductRequest entity, ResolveProductRequestRequest request, CancellationToken cancellationToken)
    {
        var explicitUrl = Trimmed(request.ImageUrl);

        if (explicitUrl is not null)
            return explicitUrl;

        if (!request.UseSourceBottleImage || entity.SourceBottleId is not Guid sourceBottleId)
            return null;

        return await db.BottleImages
            .Where(i => i.BottleId == sourceBottleId && !i.IsDeleted && !i.Bottle.IsDeleted)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .Select(i => i.Url)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductRequestDto MapToDto(ProductRequest request) => new()
    {
        Id = request.Id,
        Name = request.Name,
        Brand = request.Brand,
        DistilleryId = request.DistilleryId,
        DistilleryName = request.Distillery is { IsDeleted: false } ? request.Distillery.Name : null,
        Category = request.Category,
        Age = request.Age,
        AbvPercent = request.AbvPercent,
        VolumeMl = request.VolumeMl,
        Barcode = request.Barcode,
        Country = request.Country,
        Region = request.Region,
        UserNote = request.UserNote,
        Status = request.Status,
        AdminNote = request.AdminNote,
        RequesterId = request.UserId,
        RequesterDisplayName = request.User?.DisplayName ?? string.Empty,
        ResolvedProductId = request.ResolvedProductId,
        SourceBottleId = request.SourceBottleId,
        CreatedAt = request.CreatedAt,
        RespondedAt = request.RespondedAt,
    };
}
