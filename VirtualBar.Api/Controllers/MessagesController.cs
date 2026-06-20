using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Messages;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController(IMessageService messageService) : ControllerBase
{
    /// <summary>Sends a direct message to another collector.</summary>
    /// <param name="request">The recipient and message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Message sent; returns the created message.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Recipient not found.</response>
    [HttpPost]
    public async Task<IActionResult> Send(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await messageService.SendAsync(request, cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetConversation), new { userId = result.Data!.ReceiverId }, result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns the conversation between the current collector and another collector.</summary>
    /// <param name="userId">The other collector's identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the conversation messages.</response>
    /// <response code="400">Cannot retrieve a conversation with yourself.</response>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetConversation(Guid userId, CancellationToken cancellationToken)
    {
        var result = await messageService.GetConversationAsync(userId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Marks a received message as read.</summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Message marked as read.</response>
    /// <response code="403">Only the recipient can mark a message as read.</response>
    /// <response code="404">Message not found.</response>
    [HttpPost("{messageId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await messageService.MarkReadAsync(messageId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }
}
