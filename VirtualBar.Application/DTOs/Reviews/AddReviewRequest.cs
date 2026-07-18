using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Reviews;

public sealed class AddReviewRequest
{
    public int Score { get; set; }

    public string? Nose { get; set; }

    public string? Palate { get; set; }

    public string? Finish { get; set; }

    public string? Summary { get; set; }

    public List<FlavorTag>? Flavors { get; set; }
}
