using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductRequests;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class ProductRequestValidationDecorator(
    ProductRequestService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IProductRequestService
{
    private const int MaxOpenRequestsPerUser = 25;

    public async Task<Result<ProductRequestDto>> CreateAsync(CreateProductRequestRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ProductRequestDto>.Fail("Name is required.");

        if (!Enum.IsDefined(request.Category))
            return Result<ProductRequestDto>.Fail("Category is invalid.");

        if (!string.IsNullOrWhiteSpace(request.Barcode) && !IsValidBarcode(request.Barcode))
            return Result<ProductRequestDto>.Fail("Invalid barcode.");

        if (request.Age is < 1 or > 100)
            return Result<ProductRequestDto>.Fail("Age must be between 1 and 100 years.");

        if (request.AbvPercent is < 1 or > 96)
            return Result<ProductRequestDto>.Fail("ABV must be between 1 and 96 percent.");

        if (request.VolumeMl is < 20 or > 6000)
            return Result<ProductRequestDto>.Fail("Volume must be between 20 and 6000 millilitres.");

        if (request.Brand is { Length: > 200 })
            return Result<ProductRequestDto>.Fail("Brand must be 200 characters or fewer.");

        if (request.UserNote is { Length: > 500 })
            return Result<ProductRequestDto>.Fail("Note must be 500 characters or fewer.");

        string? distilleryName = null;

        if (request.DistilleryId is Guid distilleryId)
        {
            distilleryName = await db.Distilleries
                .Where(d => d.Id == distilleryId && !d.IsDeleted)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (distilleryName is null)
                return Result<ProductRequestDto>.Fail("Distillery not found.");
        }

        if (request.SourceBottleId is Guid sourceBottleId)
        {
            var sourceBottle = await db.Bottles
                .FirstOrDefaultAsync(b => b.Id == sourceBottleId && !b.IsDeleted, cancellationToken);

            if (sourceBottle is null)
                return Result<ProductRequestDto>.Fail("Source bottle not found.");

            if (sourceBottle.UserId != currentUser.UserId)
                return Result<ProductRequestDto>.Forbidden("Forbidden.");
        }

        var openRequests = await db.ProductRequests
            .CountAsync(r => r.UserId == currentUser.UserId
                && r.Status == ProductRequestStatus.Pending
                && !r.IsDeleted, cancellationToken);

        if (openRequests >= MaxOpenRequestsPerUser)
            return Result<ProductRequestDto>.Fail("You have too many open catalog requests.");

        // Friendly fast paths only — the filtered unique index on CanonicalKey is the real dedupe guard.
        var canonicalKey = ProductKey.For(
            distilleryName ?? request.Brand,
            request.Name.Trim(),
            request.Category,
            request.Age,
            null,
            request.VolumeMl);

        var alreadyInCatalog = await db.Products
            .AnyAsync(p => p.CanonicalKey == canonicalKey && !p.IsDeleted, cancellationToken);

        if (alreadyInCatalog)
            return Result<ProductRequestDto>.Conflict("This product already exists in the catalog.");

        var alreadyRequested = await db.ProductRequests
            .AnyAsync(r => r.CanonicalKey == canonicalKey
                && r.Status == ProductRequestStatus.Pending
                && !r.IsDeleted, cancellationToken);

        if (alreadyRequested)
            return Result<ProductRequestDto>.Conflict("This product has already been requested.");

        return await inner.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<List<ProductRequestDto>>> GetMineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetMineAsync(cancellationToken);
    }

    public async Task<Result<bool>> WithdrawAsync(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await db.ProductRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);

        if (entity is null)
            return Result<bool>.NotFound("Product request not found.");

        if (entity.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        if (entity.Status != ProductRequestStatus.Pending)
            return Result<bool>.Conflict("Only pending requests can be withdrawn.");

        return await inner.WithdrawAsync(requestId, cancellationToken);
    }

    public async Task<Result<List<ProductRequestDto>>> GetAllAsync(ProductRequestStatus? status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<List<ProductRequestDto>>.Forbidden("Only administrators can manage product requests.");

        return await inner.GetAllAsync(status, cancellationToken);
    }

    public async Task<Result<ProductRequestDto>> ApproveAsync(Guid requestId, ResolveProductRequestRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<ProductRequestDto>.Forbidden("Only administrators can manage product requests.");

        var entity = await db.ProductRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);

        if (entity is null)
            return Result<ProductRequestDto>.NotFound("Product request not found.");

        if (entity.Status != ProductRequestStatus.Pending)
            return Result<ProductRequestDto>.Conflict("Request already resolved.");

        if (request.ExistingProductId is Guid existingProductId)
        {
            var productExists = await db.Products
                .AnyAsync(p => p.Id == existingProductId && !p.IsDeleted, cancellationToken);

            if (!productExists)
                return Result<ProductRequestDto>.Fail("Product not found.");
        }
        else
        {
            var name = request.Name ?? entity.Name;
            var category = request.Category ?? entity.Category;
            var barcode = request.Barcode ?? entity.Barcode;
            var age = request.Age ?? entity.Age;
            var abvPercent = request.AbvPercent ?? entity.AbvPercent;
            var volumeMl = request.VolumeMl ?? entity.VolumeMl;
            var distilleryId = request.DistilleryId ?? entity.DistilleryId;

            if (string.IsNullOrWhiteSpace(name))
                return Result<ProductRequestDto>.Fail("Name is required.");

            if (!Enum.IsDefined(category))
                return Result<ProductRequestDto>.Fail("Category is invalid.");

            if (!string.IsNullOrWhiteSpace(barcode) && !IsValidBarcode(barcode))
                return Result<ProductRequestDto>.Fail("Invalid barcode.");

            if (age is < 1 or > 100)
                return Result<ProductRequestDto>.Fail("Age must be between 1 and 100 years.");

            if (abvPercent is < 1 or > 96)
                return Result<ProductRequestDto>.Fail("ABV must be between 1 and 96 percent.");

            if (volumeMl is < 20 or > 6000)
                return Result<ProductRequestDto>.Fail("Volume must be between 20 and 6000 millilitres.");

            if (distilleryId is Guid effectiveDistilleryId)
            {
                var distilleryExists = await db.Distilleries
                    .AnyAsync(d => d.Id == effectiveDistilleryId && !d.IsDeleted, cancellationToken);

                if (!distilleryExists)
                    return Result<ProductRequestDto>.Fail("Distillery not found.");
            }
        }

        return await inner.ApproveAsync(requestId, request, cancellationToken);
    }

    public async Task<Result<ProductRequestDto>> RejectAsync(Guid requestId, RejectProductRequestRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<ProductRequestDto>.Forbidden("Only administrators can manage product requests.");

        var entity = await db.ProductRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, cancellationToken);

        if (entity is null)
            return Result<ProductRequestDto>.NotFound("Product request not found.");

        if (entity.Status != ProductRequestStatus.Pending)
            return Result<ProductRequestDto>.Conflict("Request already resolved.");

        return await inner.RejectAsync(requestId, request, cancellationToken);
    }

    private static bool IsValidBarcode(string barcode)
    {
        var trimmed = barcode.Trim();

        return trimmed.Length is >= 8 and <= 14 && trimmed.All(char.IsAsciiDigit);
    }
}
