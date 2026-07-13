using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BottlesController(IBottleService bottleService) : ControllerBase
{
    /// <summary>Returns all bottles in a collector's public bar.</summary>
    /// <param name="userId">The collector's user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the list of bottles.</response>
    /// <response code="400">User ID is missing or invalid.</response>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetBottlesByUser([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var result = await bottleService.GetBottlesByUserAsync(userId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns a single bottle by identifier.</summary>
    /// <param name="id">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the bottle.</response>
    /// <response code="404">The bottle does not exist.</response>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBottleById(Guid id, CancellationToken cancellationToken)
    {
        var result = await bottleService.GetBottleByIdAsync(id, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns all bottles currently listed for sale in the marketplace.</summary>
    /// <param name="query">Optional filters: search text, category, sort order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the matching bottles.</response>
    [AllowAnonymous]
    [HttpGet("marketplace")]
    public async Task<IActionResult> GetMarketplace([FromQuery] MarketplaceQuery query, CancellationToken cancellationToken)
    {
        var result = await bottleService.GetMarketplaceAsync(query, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Adds a new bottle to the current collector's bar.</summary>
    /// <param name="request">The bottle details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Bottle created; returns the created bottle.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPost]
    public async Task<IActionResult> AddBottle(AddBottleRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleService.AddBottleAsync(request, cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetBottleById), new { id = result.Data!.Id }, result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Updates an existing bottle owned by the current collector.</summary>
    /// <param name="id">The bottle identifier.</param>
    /// <param name="request">The updated bottle details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Bottle updated; returns the updated bottle.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The bottle belongs to another collector.</response>
    /// <response code="404">The bottle does not exist.</response>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBottle(Guid id, UpdateBottleRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleService.UpdateBottleAsync(id, request, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Lists a bottle for sale with a price and currency.</summary>
    /// <param name="id">The bottle identifier.</param>
    /// <param name="request">The asking price and currency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Bottle listed for sale.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The bottle belongs to another collector.</response>
    /// <response code="404">The bottle does not exist.</response>
    [HttpPost("{id:guid}/list-for-sale")]
    public async Task<IActionResult> ListForSale(Guid id, ListForSaleRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleService.ListForSaleAsync(id, request, cancellationToken);
        return result.Success 
            ? Ok() 
            : result.ToActionResult(this);
    }

    /// <summary>Removes a bottle from the sale listing.</summary>
    /// <param name="id">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Bottle unlisted from sale.</response>
    /// <response code="400">Bottle is not currently listed for sale.</response>
    /// <response code="403">The bottle belongs to another collector.</response>
    /// <response code="404">The bottle does not exist.</response>
    [HttpPost("{id:guid}/unlist")]
    public async Task<IActionResult> UnlistFromSale(Guid id, CancellationToken cancellationToken)
    {
        var result = await bottleService.UnlistFromSaleAsync(id, cancellationToken);
        return result.Success 
            ? Ok() 
            : result.ToActionResult(this);
    }

    /// <summary>Reorders the current collector's bottles to the given identifier sequence.</summary>
    /// <param name="request">The bottle identifiers in the desired display order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Bottles reordered.</response>
    /// <response code="400">Bottle IDs are missing or contain duplicates.</response>
    /// <response code="404">A bottle does not exist or belongs to another collector.</response>
    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderBottles(ReorderBottlesRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleService.ReorderBottlesAsync(request, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }

    /// <summary>Removes a bottle owned by the current collector.</summary>
    /// <param name="id">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Bottle removed.</response>
    /// <response code="403">The bottle belongs to another collector.</response>
    /// <response code="404">The bottle does not exist.</response>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveBottle(Guid id, CancellationToken cancellationToken)
    {
        var result = await bottleService.RemoveBottleAsync(id, cancellationToken);
        return result.Success 
            ? Ok() 
            : result.ToActionResult(this);
    }
}
