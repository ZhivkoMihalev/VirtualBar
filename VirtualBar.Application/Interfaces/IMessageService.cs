using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Messages;

namespace VirtualBar.Application.Interfaces;

public interface IMessageService
{
    Task<Result<List<ConversationSummaryDto>>> GetInboxAsync(CancellationToken cancellationToken);

    Task<Result<MessageDto>> SendAsync(SendMessageRequest request, CancellationToken cancellationToken);

    Task<Result<List<MessageDto>>> GetConversationAsync(Guid withUserId, CancellationToken cancellationToken);

    Task<Result<bool>> MarkReadAsync(Guid messageId, CancellationToken cancellationToken);
}
