using Microsoft.AspNetCore.Identity;

namespace VirtualBar.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Bottle> Bottles { get; set; } = [];

    public ICollection<BottleLike> Likes { get; set; } = [];

    public ICollection<BottleComment> Comments { get; set; } = [];

    public ICollection<UserFollow> Followers { get; set; } = [];

    public ICollection<UserFollow> Following { get; set; } = [];

    public ICollection<Message> SentMessages { get; set; } = [];

    public ICollection<Message> ReceivedMessages { get; set; } = [];
}
