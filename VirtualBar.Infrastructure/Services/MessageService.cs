using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Messages;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class MessageService(
    AppDbContext db,
    ICurrentUser currentUser) : IMessageService
{
    public async Task<Result<List<ConversationSummaryDto>>> GetInboxAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        var messages = await db.Messages
            .Where(m => !m.IsDeleted && (m.SenderId == userId || m.ReceiverId == userId))
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var conversations = messages
            .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
            .Select(g =>
            {
                var lastMsg = g.First();
                var otherUser = lastMsg.SenderId == userId ? lastMsg.Receiver : lastMsg.Sender;
                return new ConversationSummaryDto
                {
                    OtherUserId = otherUser.Id,
                    OtherUserDisplayName = otherUser.DisplayName,
                    OtherUserAvatarUrl = otherUser.AvatarUrl,
                    LastMessageContent = lastMsg.Content,
                    LastMessageAt = lastMsg.CreatedAt,
                    LastMessageIsFromMe = lastMsg.SenderId == userId,
                    UnreadCount = g.Count(m => m.ReceiverId == userId && !m.IsRead)
                };
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();

        return Result<List<ConversationSummaryDto>>.Ok(conversations);
    }

    public async Task<Result<MessageDto>> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            SenderId = currentUser.UserId,
            ReceiverId = request.ReceiverId,
            Content = request.Content
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(message).Reference(m => m.Sender).LoadAsync(cancellationToken);

        return Result<MessageDto>.Ok(MapToDto(message));
    }

    public async Task<Result<List<MessageDto>>> GetConversationAsync(Guid withUserId, CancellationToken cancellationToken)
    {
        var messages = await db.Messages
            .Where(m => !m.IsDeleted &&
                ((m.SenderId == currentUser.UserId && m.ReceiverId == withUserId) ||
                 (m.SenderId == withUserId && m.ReceiverId == currentUser.UserId)))
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<MessageDto>>.Ok(messages.Select(MapToDto).ToList());
    }

    public async Task<Result<bool>> MarkReadAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, cancellationToken);

        message!.IsRead = true;
        message.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    private static MessageDto MapToDto(Message m) => new()
    {
        Id = m.Id,
        SenderId = m.SenderId,
        SenderDisplayName = m.Sender.DisplayName,
        ReceiverId = m.ReceiverId,
        Content = m.Content,
        IsRead = m.IsRead,
        CreatedAt = m.CreatedAt
    };
}
