using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Messages;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class MessageService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService) : IMessageService
{
    public async Task<Result<List<ConversationSummaryDto>>> GetInboxAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        var grouped = await db.Messages
            .Where(m => !m.IsDeleted && (m.SenderId == userId || m.ReceiverId == userId))
            .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
            .Select(g => new
            {
                OtherUserId         = g.Key,
                LastMessageAt       = g.Max(m => m.CreatedAt),
                UnreadCount         = g.Count(m => m.ReceiverId == userId && !m.IsRead),
                LastMessageContent  = g.OrderByDescending(m => m.CreatedAt).Select(m => m.Content).First(),
                LastMessageIsFromMe = g.OrderByDescending(m => m.CreatedAt).Select(m => m.SenderId == userId).First(),
            })
            .OrderByDescending(g => g.LastMessageAt)
            .ToListAsync(cancellationToken);

        if (grouped.Count == 0)
            return Result<List<ConversationSummaryDto>>.Ok([]);

        var otherUserIds = grouped.Select(g => g.OtherUserId).ToList();
        var users = await db.Users
            .Where(u => otherUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(cancellationToken);
        var userMap = users.ToDictionary(u => u.Id);

        var conversations = grouped.Select(g =>
        {
            var other = userMap[g.OtherUserId];
            return new ConversationSummaryDto
            {
                OtherUserId          = g.OtherUserId,
                OtherUserDisplayName = other.DisplayName,
                OtherUserAvatarUrl   = other.AvatarUrl,
                LastMessageAt        = g.LastMessageAt,
                LastMessageContent   = g.LastMessageContent,
                LastMessageIsFromMe  = g.LastMessageIsFromMe,
                UnreadCount          = g.UnreadCount,
            };
        }).ToList();

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

        await notificationService.CreateAsync(request.ReceiverId, NotificationType.NewMessage, message.Id, null, cancellationToken);

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
