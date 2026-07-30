using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.ProductRequests;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/product-requests")]
[Authorize]
public sealed class ProductRequestsController(IProductRequestService productRequestService) : ControllerBase
{
    /// <summary>Files a request to add a missing product to the canonical catalog.</summary>
    /// <param name="request">The proposed product fields and an optional note for the administrator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Request filed; returns the created request.</response>
    /// <response code="400">Validation failed, or the open-request cap was reached.</response>
    /// <response code="403">The source bottle belongs to another collector.</response>
    /// <response code="409">The product already exists in the catalog or has already been requested.</response>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await productRequestService.CreateAsync(request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns the catalog requests filed by the current collector, newest first.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the current collector's requests.</response>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await productRequestService.GetMineAsync(cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Withdraws a pending catalog request filed by the current collector.</summary>
    /// <param name="id">The product request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Request withdrawn.</response>
    /// <response code="403">The request belongs to another collector.</response>
    /// <response code="404">The request does not exist.</response>
    /// <response code="409">The request is no longer pending.</response>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        var result = await productRequestService.WithdrawAsync(id, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }

    /// <summary>Returns the administrator review queue, optionally filtered by status, newest first.</summary>
    /// <param name="status">Optional status filter: Pending, Approved or Rejected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the matching requests.</response>
    /// <response code="403">The current collector is not an administrator.</response>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ProductRequestStatus? status, CancellationToken cancellationToken)
    {
        var result = await productRequestService.GetAllAsync(status, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Approves a pending request: creates or links the catalog product and links matching bottles.</summary>
    /// <param name="id">The product request identifier.</param>
    /// <param name="request">Optional field overrides, an existing product to link, and the image reuse flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Request approved; returns the resolved request.</response>
    /// <response code="400">Validation failed, or the product to link does not exist.</response>
    /// <response code="403">The current collector is not an administrator.</response>
    /// <response code="404">The request does not exist.</response>
    /// <response code="409">The request is already resolved, or an identical product already exists.</response>
    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ResolveProductRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await productRequestService.ApproveAsync(id, request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Rejects a pending request and notifies the requester.</summary>
    /// <param name="id">The product request identifier.</param>
    /// <param name="request">An optional note explaining the rejection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Request rejected; returns the resolved request.</response>
    /// <response code="403">The current collector is not an administrator.</response>
    /// <response code="404">The request does not exist.</response>
    /// <response code="409">The request is already resolved.</response>
    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProductRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await productRequestService.RejectAsync(id, request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }
}
