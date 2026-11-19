using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Offers;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class OfferService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService,
    IBadgeService badgeService,
    IWebHostEnvironment env,
    ILogger<OfferService> logger) : IOfferService
{
    public const string NoBottleLeftMessage = "No bottle left to sell.";

    private const string BottleUploadsPath = "/uploads/bottles/";

    public async Task<Result<OfferDto>> CreateAsync(CreateOfferRequest request, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == request.BottleId && !b.IsDeleted, cancellationToken);

        var buyer = await db.Users
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);

        var offer = new Offer
        {
            BottleId = request.BottleId,
            BuyerId = currentUser.UserId,
            SellerId = bottle!.UserId,
            OfferedPrice = request.OfferedPrice,
            Currency = SupportedCurrencies.ToCode(request.Currency),
            Message = request.Message,
            Status = OfferStatus.Pending,
        };

        db.Offers.Add(offer);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the create race: a concurrent request slipped past the decorator's pre-check and the
            // filtered unique index (one pending offer per buyer per bottle) rejected this insert.
            return Result<OfferDto>.Conflict("You already have a pending offer on this bottle.");
        }

        await notificationService.CreateAsync(offer.SellerId, NotificationType.OfferReceived, offer.Id, bottle.Name, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer, bottle.Name, buyer?.DisplayName ?? "", bottle.User.DisplayName));
    }

    public async Task<Result<List<OfferDto>>> GetReceivedAsync(CancellationToken cancellationToken)
    {
        var offers = await db.Offers
            .Where(o => o.SellerId == currentUser.UserId && !o.IsDeleted)
            .Include(o => o.Bottle)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<OfferDto>>.Ok(offers.Select(o => Map(o)).ToList());
    }

    public async Task<Result<List<OfferDto>>> GetSentAsync(CancellationToken cancellationToken)
    {
        var offers = await db.Offers
            .Where(o => o.BuyerId == currentUser.UserId && !o.IsDeleted)
            .Include(o => o.Bottle)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<OfferDto>>.Ok(offers.Select(o => Map(o)).ToList());
    }

    public async Task<Result<OfferDto>> AcceptAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var respondedAt = DateTime.UtcNow;

        // The claim, the reservation and the declined offers stand or fall together; leaving early rolls them back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Atomic claim: only a still-pending row transitions, so a concurrent accept/decline/withdraw
        // race has exactly one winner — the loser sees zero affected rows and fires no notification.
        var claimed = await db.Offers
            .Where(o => o.Id == offerId && !o.IsDeleted && o.Status == OfferStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.AwaitingReceipt)
                .SetProperty(o => o.RespondedAt, respondedAt)
                .SetProperty(o => o.UpdatedAt, respondedAt), cancellationToken);

        if (claimed == 0)
            return Result<OfferDto>.Conflict("Only pending offers can be accepted.");

        var bottleId = await db.Offers
            .Where(o => o.Id == offerId)
            .Select(o => o.BottleId)
            .FirstAsync(cancellationToken);

        // Reserve one free bottle for the buyer (#130) in one conditional update, so two accepts racing for the last
        // free bottle have one winner, like the claim above. A listed bottle also comes off sale: every SET reads the
        // row as it was before the update, so the last listed bottle unlists the entry as UnlistFromSaleAsync does.
        var reserved = await db.Bottles
            .Where(b => b.Id == bottleId && !b.IsDeleted && b.Quantity - b.ReservedQuantity >= 1)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity + 1)
                .SetProperty(b => b.ForSaleQuantity, b => b.IsForSale && b.ForSaleQuantity > 0 ? b.ForSaleQuantity - 1 : b.ForSaleQuantity)
                .SetProperty(b => b.IsForSale, b => b.IsForSale && b.ForSaleQuantity > 1)
                .SetProperty(b => b.AskingPrice, b => b.IsForSale && b.ForSaleQuantity > 1 ? b.AskingPrice : null)
                .SetProperty(b => b.Currency, b => b.IsForSale && b.ForSaleQuantity > 1 ? b.Currency : null)
                .SetProperty(b => b.ForSaleAt, b => b.IsForSale && b.ForSaleQuantity > 1 ? b.ForSaleAt : null)
                .SetProperty(b => b.UpdatedAt, respondedAt), cancellationToken);

        if (reserved == 0)
            return Result<OfferDto>.Conflict(NoBottleLeftMessage, OfferErrorReasons.NoBottleLeft);

        var declined = await DeclineOffersOnSoldOutBottleAsync(bottleId, offerId, respondedAt, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var offer = await LoadFreshAsync(offerId, cancellationToken);

        await notificationService.CreateAsync(offer.BuyerId, NotificationType.OfferAccepted, offer.Id, offer.Bottle.Name, cancellationToken);

        foreach (var (declinedOfferId, buyerId) in declined)
            await notificationService.CreateAsync(buyerId, NotificationType.OfferDeclined, declinedOfferId, offer.Bottle.Name, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    public async Task<Result<OfferDto>> DeclineAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var respondedAt = DateTime.UtcNow;

        var claimed = await db.Offers
            .Where(o => o.Id == offerId && !o.IsDeleted && o.Status == OfferStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.Declined)
                .SetProperty(o => o.RespondedAt, respondedAt)
                .SetProperty(o => o.UpdatedAt, respondedAt), cancellationToken);

        if (claimed == 0)
            return Result<OfferDto>.Conflict("Only pending offers can be declined.");

        var offer = await LoadFreshAsync(offerId, cancellationToken);

        await notificationService.CreateAsync(offer.BuyerId, NotificationType.OfferDeclined, offer.Id, offer.Bottle.Name, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    public async Task<Result<OfferDto>> WithdrawAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var claimed = await db.Offers
            .Where(o => o.Id == offerId && !o.IsDeleted && o.Status == OfferStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.Withdrawn)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), cancellationToken);

        if (claimed == 0)
            return Result<OfferDto>.Conflict("Only pending offers can be withdrawn.");

        var offer = await LoadFreshAsync(offerId, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    public async Task<Result<OfferDto>> ConfirmReceiptAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var completedAt = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await db.Offers
            .Where(o => o.Id == offerId && !o.IsDeleted && o.Status == OfferStatus.AwaitingReceipt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.Accepted)
                .SetProperty(o => o.CompletedAt, completedAt)
                .SetProperty(o => o.UpdatedAt, completedAt), cancellationToken);

        if (claimed == 0)
            return Result<OfferDto>.Conflict("Only a deal awaiting receipt can be confirmed.");

        var offer = await LoadFreshAsync(offerId, cancellationToken);

        var sold = await db.Bottles
            .AsNoTracking()
            .Include(b => b.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(b => b.Product)
            .Include(b => b.Distillery)
            .FirstAsync(b => b.Id == offer.BottleId, cancellationToken);

        // The reserved bottle leaves the seller's entry, and the entry itself goes with its last bottle (#130).
        var released = await db.Bottles
            .Where(b => b.Id == offer.BottleId && !b.IsDeleted && b.ReservedQuantity >= 1)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.Quantity, b => b.Quantity - 1)
                .SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - 1)
                .SetProperty(b => b.IsDeleted, b => b.Quantity <= 1)
                .SetProperty(b => b.DeletedAt, b => b.Quantity <= 1 ? completedAt : b.DeletedAt)
                .SetProperty(b => b.UpdatedAt, completedAt), cancellationToken);

        if (released == 0)
            return Result<OfferDto>.Conflict("The reserved bottle is no longer in the seller's bar.");

        var bought = CopyForBuyer(sold, offer);
        db.Bottles.Add(bought);

        if (bought.Barcode is string barcode && bought.ProductId is Guid productId)
            await ProductBarcodeRecorder.RecordAsync(db, productId, barcode, bought.UserId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await CopyPrimaryImageAsync(sold, bought.Id, cancellationToken);

        await notificationService.CreateAsync(offer.SellerId, NotificationType.SaleCompleted, offer.Id, sold.Name, cancellationToken);

        var followerIds = await db.UserFollows
            .Where(f => f.FollowedId == offer.BuyerId)
            .Select(f => f.FollowerId)
            .ToListAsync(cancellationToken);

        await notificationService.CreateBulkAsync(followerIds, NotificationType.NewBottleFromFollowing, bought.Id, bought.Name, cancellationToken);

        // A completed deal is what FirstSale / FirstPurchase count, so they are evaluated here, not on accept.
        await badgeService.EvaluateAsync(offer.SellerId, BadgeTrigger.OfferAccepted, cancellationToken);
        await badgeService.EvaluateAsync(offer.BuyerId, BadgeTrigger.OfferAccepted, cancellationToken);
        await badgeService.EvaluateAsync(offer.BuyerId, BadgeTrigger.BottleAdded, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    public async Task<Result<OfferDto>> CancelAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var cancelledAt = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await db.Offers
            .Where(o => o.Id == offerId && !o.IsDeleted && o.Status == OfferStatus.AwaitingReceipt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.Cancelled)
                .SetProperty(o => o.CancelledAt, cancelledAt)
                .SetProperty(o => o.UpdatedAt, cancelledAt), cancellationToken);

        if (claimed == 0)
            return Result<OfferDto>.Conflict("Only a deal awaiting receipt can be cancelled.");

        var offer = await LoadFreshAsync(offerId, cancellationToken);

        // The bottle stays with the seller and off sale; listing it again is the seller's own call.
        await db.Bottles
            .Where(b => b.Id == offer.BottleId && b.ReservedQuantity >= 1)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - 1)
                .SetProperty(b => b.UpdatedAt, cancelledAt), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var otherSide = offer.BuyerId == currentUser.UserId ? offer.SellerId : offer.BuyerId;
        await notificationService.CreateAsync(otherSide, NotificationType.DealCancelled, offer.Id, offer.Bottle.Name, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    /// <summary>
    /// Declines the other pending offers on a bottle once none of it is free (#130): nobody else can buy it now.
    /// One conditional update declines them; the ones it declined are read back by their decline time, so an offer
    /// withdrawn meanwhile is neither declined nor told it was.
    /// </summary>
    private async Task<List<(Guid OfferId, Guid BuyerId)>> DeclineOffersOnSoldOutBottleAsync(
        Guid bottleId, Guid acceptedOfferId, DateTime respondedAt, CancellationToken cancellationToken)
    {
        var soldOut = await db.Bottles
            .AnyAsync(b => b.Id == bottleId && b.Quantity - b.ReservedQuantity < 1, cancellationToken);

        if (!soldOut)
            return [];

        await db.Offers
            .Where(o => o.BottleId == bottleId && o.Id != acceptedOfferId && !o.IsDeleted && o.Status == OfferStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.Declined)
                .SetProperty(o => o.RespondedAt, respondedAt)
                .SetProperty(o => o.UpdatedAt, respondedAt), cancellationToken);

        var declined = await db.Offers
            .Where(o => o.BottleId == bottleId && o.Id != acceptedOfferId && o.Status == OfferStatus.Declined && o.RespondedAt == respondedAt)
            .Select(o => new { o.Id, o.BuyerId })
            .ToListAsync(cancellationToken);

        return [.. declined.Select(o => (o.Id, o.BuyerId))];
    }

    /// <summary>
    /// The buyer's bottle: the sold one's identity, one bottle, not for sale, remembering its deal (#130). A deleted
    /// product or distillery is left out rather than copied, and the seller's own description stays with the seller.
    /// </summary>
    private static Bottle CopyForBuyer(Bottle sold, Offer offer) => new()
    {
        UserId = offer.BuyerId,
        Name = sold.Name,
        DistilleryId = sold.Distillery is { IsDeleted: false } ? sold.DistilleryId : null,
        ProductId = sold.Product is { IsDeleted: false } ? sold.ProductId : null,
        Region = sold.Region,
        Country = sold.Country,
        Category = sold.Category,
        Age = sold.Age,
        VintageYear = sold.VintageYear,
        AbvPercent = sold.AbvPercent,
        VolumeMl = sold.VolumeMl,
        Barcode = sold.Barcode,
        Condition = sold.Condition,
        IsLimited = sold.IsLimited,
        Quantity = 1,
        AcquiredFromOfferId = offer.Id,
    };

    /// <summary>
    /// Gives the buyer's bottle the sold bottle's cover image. An uploaded file is copied, so the seller deleting their
    /// own image later cannot take the buyer's with it; any other URL is kept as it is. Only a file inside the bottle
    /// uploads folder is ever copied, whatever the stored URL says. A failure is logged: the deal is already complete.
    /// </summary>
    private async Task CopyPrimaryImageAsync(Bottle sold, Guid boughtId, CancellationToken cancellationToken)
    {
        var image = sold.Images.FirstOrDefault(i => i.IsPrimary) ?? sold.Images.FirstOrDefault();
        if (image is null)
            return;

        try
        {
            var url = image.Url;

            if (url.StartsWith(BottleUploadsPath, StringComparison.Ordinal))
            {
                var uploadsDir = Path.GetFullPath(Path.Combine(
                    env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "bottles"));
                var source = Path.GetFullPath(Path.Combine(uploadsDir, url[BottleUploadsPath.Length..]));

                if (!source.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(source))
                    return;

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(source)}";
                File.Copy(source, Path.Combine(uploadsDir, fileName));
                url = BottleUploadsPath + fileName;
            }

            db.BottleImages.Add(new BottleImage { BottleId = boughtId, Url = url, IsPrimary = true, SortOrder = 0 });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not give bottle {BoughtId} the image of bottle {SoldId}.", boughtId, sold.Id);
        }
    }

    /// <summary>
    /// Reloads the offer with its navigations AFTER a successful conditional update. AsNoTracking bypasses
    /// the identity map, because the validation decorator has already tracked this offer (same scoped
    /// context) with its stale pre-update status. The row provably exists — it was just updated.
    /// </summary>
    private Task<Offer> LoadFreshAsync(Guid offerId, CancellationToken cancellationToken) =>
        db.Offers
            .AsNoTracking()
            .Include(o => o.Bottle)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .FirstAsync(o => o.Id == offerId, cancellationToken);

    private static OfferDto Map(Offer offer, string bottleName, string buyerName, string sellerName) => new()
    {
        Id = offer.Id,
        BottleId = offer.BottleId,
        BottleName = bottleName,
        BuyerId = offer.BuyerId,
        BuyerDisplayName = buyerName,
        SellerId = offer.SellerId,
        SellerDisplayName = sellerName,
        OfferedPrice = offer.OfferedPrice,
        Currency = offer.Currency,
        Message = offer.Message,
        Status = offer.Status,
        RespondedAt = offer.RespondedAt,
        CompletedAt = offer.CompletedAt,
        CancelledAt = offer.CancelledAt,
        CreatedAt = offer.CreatedAt,
    };

    private static OfferDto Map(Offer offer) => new()
    {
        Id = offer.Id,
        BottleId = offer.BottleId,
        BottleName = offer.Bottle!.Name,
        BuyerId = offer.BuyerId,
        BuyerDisplayName = offer.Buyer!.DisplayName,
        SellerId = offer.SellerId,
        SellerDisplayName = offer.Seller!.DisplayName,
        OfferedPrice = offer.OfferedPrice,
        Currency = offer.Currency,
        Message = offer.Message,
        Status = offer.Status,
        RespondedAt = offer.RespondedAt,
        CompletedAt = offer.CompletedAt,
        CancelledAt = offer.CancelledAt,
        CreatedAt = offer.CreatedAt,
    };
}
