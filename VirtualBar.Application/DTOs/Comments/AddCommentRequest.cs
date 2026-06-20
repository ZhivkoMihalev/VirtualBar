using System.ComponentModel.DataAnnotations;

namespace VirtualBar.Application.DTOs.Comments;

public sealed class AddCommentRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}
