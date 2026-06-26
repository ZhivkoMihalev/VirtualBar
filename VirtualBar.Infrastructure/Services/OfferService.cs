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
    INotificationService notificationService) : IOfferService
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
        await db.SaveChangesAsync(cancellationToken);

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
        var offer = await db.Offers
            .Include(o => o.Bottle)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        offer!.Status = OfferStatus.Accepted;
        offer.RespondedAt = DateTime.UtcNow;
        offer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await notificationService.CreateAsync(offer.BuyerId, NotificationType.OfferAccepted, offer.Id, offer.Bottle.Name, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    public async Task<Result<OfferDto>> DeclineAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await db.Offers
            .Include(o => o.Bottle)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        offer!.Status = OfferStatus.Declined;
        offer.RespondedAt = DateTime.UtcNow;
        offer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await notificationService.CreateAsync(offer.BuyerId, NotificationType.OfferDeclined, offer.Id, offer.Bottle.Name, cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

    public async Task<Result<OfferDto>> WithdrawAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await db.Offers
            .Include(o => o.Bottle)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        offer!.Status = OfferStatus.Withdrawn;
        offer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<OfferDto>.Ok(Map(offer));
    }

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
