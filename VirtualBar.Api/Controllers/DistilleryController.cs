using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/distilleries")]
[Authorize]
public sealed class DistilleryController(IDistilleryService distilleryService) : ControllerBase
{
    /// <summary>Returns all distilleries ordered by name, optionally filtered by spirit category.</summary>
    /// <param name="category">Optional spirit category filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of distilleries.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] SpiritCategory? category, CancellationToken cancellationToken)
    {
        var result = await distilleryService.GetAllAsync(category, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }
}
