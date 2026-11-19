using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Offers;

namespace VirtualBar.Application.Interfaces;

public interface IOfferService
{
    Task<Result<OfferDto>> CreateAsync(CreateOfferRequest request, CancellationToken cancellationToken);

    Task<Result<List<OfferDto>>> GetReceivedAsync(CancellationToken cancellationToken);

    Task<Result<List<OfferDto>>> GetSentAsync(CancellationToken cancellationToken);

    Task<Result<OfferDto>> AcceptAsync(Guid offerId, CancellationToken cancellationToken);

    Task<Result<OfferDto>> DeclineAsync(Guid offerId, CancellationToken cancellationToken);

    Task<Result<OfferDto>> WithdrawAsync(Guid offerId, CancellationToken cancellationToken);

    /// <summary>
    /// The buyer confirms they received the bottle of an accepted deal (#130): the deal is completed, one bottle
    /// leaves the seller's entry and a copy of it joins the buyer's bar.
    /// </summary>
    Task<Result<OfferDto>> ConfirmReceiptAsync(Guid offerId, CancellationToken cancellationToken);

    /// <summary>Either side ends a deal that awaits the buyer's confirmation (#130); the reserved bottle is freed.</summary>
    Task<Result<OfferDto>> CancelAsync(Guid offerId, CancellationToken cancellationToken);
}
