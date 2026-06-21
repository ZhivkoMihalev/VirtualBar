namespace VirtualBar.Application.DTOs.Messages;

public sealed class ConversationSummaryDto
{
    public Guid OtherUserId { get; set; }

    public string OtherUserDisplayName { get; set; } = string.Empty;

    public string? OtherUserAvatarUrl { get; set; }

    public string LastMessageContent { get; set; } = string.Empty;

    public DateTime LastMessageAt { get; set; }

    public bool LastMessageIsFromMe { get; set; }

    public int UnreadCount { get; set; }
}
