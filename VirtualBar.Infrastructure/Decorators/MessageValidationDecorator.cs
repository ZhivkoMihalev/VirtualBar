using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Messages;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class MessageValidationDecorator(
    MessageService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IMessageService
{
    public Task<Result<List<ConversationSummaryDto>>> GetInboxAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return inner.GetInboxAsync(cancellationToken);
    }

    public async Task<Result<MessageDto>> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<MessageDto>.Fail("Content is required.");

        if (request.ReceiverId == currentUser.UserId)
            return Result<MessageDto>.Fail("Cannot send a message to yourself.");

        var receiver = await db.Users.FindAsync(new object[] { request.ReceiverId }, cancellationToken);
        if (receiver is null)
            return Result<MessageDto>.NotFound("Recipient not found.");

        return await inner.SendAsync(request, cancellationToken);
    }

    public async Task<Result<List<MessageDto>>> GetConversationAsync(Guid withUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (withUserId == currentUser.UserId)
            return Result<List<MessageDto>>.Fail("Cannot retrieve a conversation with yourself.");

        return await inner.GetConversationAsync(withUserId, cancellationToken);
    }

    public async Task<Result<bool>> MarkReadAsync(Guid messageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, cancellationToken);
        if (message is null)
            return Result<bool>.NotFound("Message not found.");

        if (message.ReceiverId != currentUser.UserId)
            return Result<bool>.Forbidden("Only the recipient can mark a message as read.");

        return await inner.MarkReadAsync(messageId, cancellationToken);
    }
}
