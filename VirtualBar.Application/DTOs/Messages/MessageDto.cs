namespace VirtualBar.Application.DTOs.Messages;

public sealed class MessageDto
{
    public Guid Id { get; set; }

    public Guid SenderId { get; set; }

    public string SenderDisplayName { get; set; } = string.Empty;

    public Guid ReceiverId { get; set; }

    public string Content { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
