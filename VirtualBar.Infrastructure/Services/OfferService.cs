using Microsoft.EntityFrameworkCore;
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
    IBadgeService badgeService) : IOfferService
{
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
            Currency = request.Currency,
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

        // Atomic claim: only a still-pending row transitions, so a concurrent accept/decline/withdraw
        // race has exactly one winner — the loser sees zero affected rows and fires no notification.
        var claimed = await db.Offers
            .Where(o => o.Id == offerId && !o.IsDeleted && o.Status == OfferStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OfferStatus.Accepted)
                .SetProperty(o => o.RespondedAt, respondedAt)
                .SetProperty(o => o.UpdatedAt, respondedAt), cancellationToken);

        if (claimed == 0)
            return Result<OfferDto>.Conflict("Only pending offers can be accepted.");

        var offer = await LoadFreshAsync(offerId, cancellationToken);

        await notificationService.CreateAsync(offer.BuyerId, NotificationType.OfferAccepted, offer.Id, offer.Bottle.Name, cancellationToken);

        await badgeService.EvaluateAsync(offer.SellerId, BadgeTrigger.OfferAccepted, cancellationToken);
        await badgeService.EvaluateAsync(offer.BuyerId, BadgeTrigger.OfferAccepted, cancellationToken);

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
        CreatedAt = offer.CreatedAt,
    };
}
