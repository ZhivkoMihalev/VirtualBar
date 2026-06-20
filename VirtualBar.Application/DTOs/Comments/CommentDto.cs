namespace VirtualBar.Application.DTOs.Comments;

public sealed class CommentDto
{
    public Guid Id { get; set; }

    public Guid BottleId { get; set; }

    public Guid UserId { get; set; }

    public string UserDisplayName { get; set; } = string.Empty;

    public string? UserAvatarUrl { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
