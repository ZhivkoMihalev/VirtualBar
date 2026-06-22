using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    /// <summary>Returns the 30 most recent notifications for the current user plus the total unread count.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Success.</response>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var result = await notificationService.GetNotificationsAsync(cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Marks a single notification as read.</summary>
    /// <param name="id">Notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Success.</response>
    /// <response code="403">Access denied.</response>
    /// <response code="404">Notification not found.</response>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await notificationService.MarkReadAsync(id, cancellationToken);
        return result.Success 
            ? Ok() 
            : result.ToActionResult(this);
    }

    /// <summary>Marks all notifications for the current user as read.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Success.</response>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await notificationService.MarkAllReadAsync(cancellationToken);
        return result.Success 
            ? Ok() 
            : result.ToActionResult(this);
    }
}
