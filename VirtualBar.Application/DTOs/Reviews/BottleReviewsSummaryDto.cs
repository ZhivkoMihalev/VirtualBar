using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Reviews;

public sealed class BottleReviewsSummaryDto
{
    public double? AverageScore { get; set; }

    public int ReviewsCount { get; set; }

    public List<FlavorTag> TopFlavors { get; set; } = [];

    public List<BottleReviewDto> Reviews { get; set; } = [];

    public BottleReviewDto? MyReview { get; set; }
}
