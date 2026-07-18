using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Reviews;

public sealed class BottleReviewDto
{
    public Guid Id { get; set; }

    public Guid BottleId { get; set; }

    public Guid UserId { get; set; }

    public string UserDisplayName { get; set; } = string.Empty;

    public string? UserAvatarUrl { get; set; }

    public int Score { get; set; }

    public string? Nose { get; set; }

    public string? Palate { get; set; }

    public string? Finish { get; set; }

    public string? Summary { get; set; }

    public List<FlavorTag> Flavors { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
