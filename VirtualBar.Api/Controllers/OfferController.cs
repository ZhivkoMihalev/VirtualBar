using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Offers;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize]
public sealed class OfferController(IOfferService offerService) : ControllerBase
{
    /// <summary>Creates a new purchase offer for a bottle.</summary>
    /// <param name="request">The bottle, offered price, currency and optional message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Offer created; returns the created offer.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="409">A pending offer already exists for this bottle.</response>
    /// <response code="404">The bottle does not exist.</response>
    [HttpPost]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferRequest request, CancellationToken cancellationToken)
    {
        var result = await offerService.CreateAsync(request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns all offers received by the current collector as a seller.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the received offers.</response>
    [HttpGet("received")]
    public async Task<IActionResult> GetReceived(CancellationToken cancellationToken)
    {
        var result = await offerService.GetReceivedAsync(cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns all offers sent by the current collector as a buyer.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the sent offers.</response>
    [HttpGet("sent")]
    public async Task<IActionResult> GetSent(CancellationToken cancellationToken)
    {
        var result = await offerService.GetSentAsync(cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Accepts an offer received by the current collector.</summary>
    /// <param name="id">The offer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Offer accepted: one bottle is reserved for the buyer until they confirm receipt.</response>
    /// <response code="409">The offer is no longer pending, or no bottle is left to sell.</response>
    /// <response code="403">The offer was not made to the current collector.</response>
    /// <response code="404">The offer does not exist.</response>
    [HttpPatch("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var result = await offerService.AcceptAsync(id, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Declines an offer received by the current collector.</summary>
    /// <param name="id">The offer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Offer declined; returns the updated offer.</response>
    /// <response code="400">The offer is no longer pending.</response>
    /// <response code="403">The offer was not made to the current collector.</response>
    /// <response code="404">The offer does not exist.</response>
    [HttpPatch("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken)
    {
        var result = await offerService.DeclineAsync(id, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Withdraws an offer sent by the current collector.</summary>
    /// <param name="id">The offer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Offer withdrawn; returns the updated offer.</response>
    /// <response code="400">The offer is no longer pending.</response>
    /// <response code="403">The offer was not made by the current collector.</response>
    /// <response code="404">The offer does not exist.</response>
    [HttpPatch("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        var result = await offerService.WithdrawAsync(id, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Confirms that the buyer received the bottle of an accepted deal: the bottle moves to their bar.</summary>
    /// <param name="id">The offer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Deal completed; returns the updated offer.</response>
    /// <response code="403">The current collector is not the buyer.</response>
    /// <response code="404">The offer does not exist.</response>
    /// <response code="409">The deal is not awaiting the buyer's confirmation.</response>
    [HttpPatch("{id:guid}/confirm-receipt")]
    public async Task<IActionResult> ConfirmReceipt(Guid id, CancellationToken cancellationToken)
    {
        var result = await offerService.ConfirmReceiptAsync(id, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Cancels an accepted deal before the buyer confirmed receiving the bottle; it stays with the seller.</summary>
    /// <param name="id">The offer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Deal cancelled; returns the updated offer.</response>
    /// <response code="403">The current collector is neither the buyer nor the seller.</response>
    /// <response code="404">The offer does not exist.</response>
    /// <response code="409">The deal is not awaiting the buyer's confirmation.</response>
    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await offerService.CancelAsync(id, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }
}
