namespace VirtualBar.Domain.Entities;

public class Message : BaseEntity
{
    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = null!;

    public Guid ReceiverId { get; set; }
    public AppUser Receiver { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}
