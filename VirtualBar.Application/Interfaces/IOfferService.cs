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
}
