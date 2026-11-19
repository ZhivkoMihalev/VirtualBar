using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Offers;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class OfferValidationDecorator(
    IOfferService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IOfferService
{
    public async Task<Result<OfferDto>> CreateAsync(CreateOfferRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.OfferedPrice <= 0)
            return Result<OfferDto>.Fail("Offered price must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Currency))
            return Result<OfferDto>.Fail("Currency is required.");

        if (!SupportedCurrencies.IsSupported(request.Currency))
            return Result<OfferDto>.Fail(SupportedCurrencies.NotSupportedMessage);

        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == request.BottleId && !b.IsDeleted, cancellationToken);

        if (bottle is null)
            return Result<OfferDto>.NotFound("Bottle not found.");

        if (bottle.UserId == currentUser.UserId)
            return Result<OfferDto>.Fail("You cannot make an offer on your own bottle.");

        // Every bottle of the entry is reserved for buyers (#130): an offer could never be accepted.
        if (bottle.Quantity - bottle.ReservedQuantity < 1)
            return Result<OfferDto>.Conflict(OfferService.NoBottleLeftMessage, OfferErrorReasons.NoBottleLeft);

        var existingPending = await db.Offers
            .AnyAsync(o => o.BottleId == request.BottleId
                && o.BuyerId == currentUser.UserId
                && o.Status == OfferStatus.Pending
                && !o.IsDeleted, cancellationToken);

        if (existingPending)
            return Result<OfferDto>.Conflict("You already have a pending offer on this bottle.");

        return await inner.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<List<OfferDto>>> GetReceivedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.GetReceivedAsync(cancellationToken);
    }

    public async Task<Result<List<OfferDto>>> GetSentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.GetSentAsync(cancellationToken);
    }

    public async Task<Result<OfferDto>> AcceptAsync(Guid offerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var offer = await db.Offers
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        if (offer is null)
            return Result<OfferDto>.NotFound("Offer not found.");

        if (offer.SellerId != currentUser.UserId)
            return Result<OfferDto>.Forbidden("Only the seller can accept this offer.");

        if (offer.Status != OfferStatus.Pending)
            return Result<OfferDto>.Conflict("Only pending offers can be accepted.");

        // A friendly fast path: the service reserves the bottle in one conditional update, which closes the race.
        var hasFreeBottle = await db.Bottles
            .AnyAsync(b => b.Id == offer.BottleId && !b.IsDeleted && b.Quantity - b.ReservedQuantity >= 1, cancellationToken);

        if (!hasFreeBottle)
            return Result<OfferDto>.Conflict(OfferService.NoBottleLeftMessage, OfferErrorReasons.NoBottleLeft);

        return await inner.AcceptAsync(offerId, cancellationToken);
    }

    public async Task<Result<OfferDto>> DeclineAsync(Guid offerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var offer = await db.Offers
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        if (offer is null)
            return Result<OfferDto>.NotFound("Offer not found.");

        if (offer.SellerId != currentUser.UserId)
            return Result<OfferDto>.Forbidden("Only the seller can decline this offer.");

        if (offer.Status != OfferStatus.Pending)
            return Result<OfferDto>.Conflict("Only pending offers can be declined.");

        return await inner.DeclineAsync(offerId, cancellationToken);
    }

    public async Task<Result<OfferDto>> WithdrawAsync(Guid offerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var offer = await db.Offers
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        if (offer is null)
            return Result<OfferDto>.NotFound("Offer not found.");

        if (offer.BuyerId != currentUser.UserId)
            return Result<OfferDto>.Forbidden("Only the buyer can withdraw this offer.");

        if (offer.Status != OfferStatus.Pending)
            return Result<OfferDto>.Conflict("Only pending offers can be withdrawn.");

        return await inner.WithdrawAsync(offerId, cancellationToken);
    }

    public async Task<Result<OfferDto>> ConfirmReceiptAsync(Guid offerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var offer = await db.Offers
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        if (offer is null)
            return Result<OfferDto>.NotFound("Offer not found.");

        if (offer.BuyerId != currentUser.UserId)
            return Result<OfferDto>.Forbidden("Only the buyer can confirm receiving the bottle.");

        if (offer.Status != OfferStatus.AwaitingReceipt)
            return Result<OfferDto>.Conflict("Only a deal awaiting receipt can be confirmed.");

        return await inner.ConfirmReceiptAsync(offerId, cancellationToken);
    }

    public async Task<Result<OfferDto>> CancelAsync(Guid offerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var offer = await db.Offers
            .FirstOrDefaultAsync(o => o.Id == offerId && !o.IsDeleted, cancellationToken);

        if (offer is null)
            return Result<OfferDto>.NotFound("Offer not found.");

        if (offer.BuyerId != currentUser.UserId && offer.SellerId != currentUser.UserId)
            return Result<OfferDto>.Forbidden("Only the buyer or the seller can cancel this deal.");

        if (offer.Status != OfferStatus.AwaitingReceipt)
            return Result<OfferDto>.Conflict("Only a deal awaiting receipt can be cancelled.");

        return await inner.CancelAsync(offerId, cancellationToken);
    }
}
