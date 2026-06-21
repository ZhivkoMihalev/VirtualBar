using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Feed;

namespace VirtualBar.Application.Interfaces;

public interface IFeedService
{
    Task<Result<List<FeedItemDto>>> GetFeedAsync(int skip, int take, CancellationToken cancellationToken);
}
