using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Reviews;

namespace VirtualBar.Application.Interfaces;

public interface IBottleReviewService
{
    Task<Result<BottleReviewsSummaryDto>> GetReviewsAsync(Guid bottleId, CancellationToken cancellationToken);

    Task<Result<BottleReviewDto>> AddReviewAsync(Guid bottleId, AddReviewRequest request, CancellationToken cancellationToken);

    Task<Result<BottleReviewDto>> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> DeleteReviewAsync(Guid reviewId, CancellationToken cancellationToken);
}
