using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Messages;

public sealed class SendMessageRequest
{
    [Required]
    public Guid ReceiverId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}
