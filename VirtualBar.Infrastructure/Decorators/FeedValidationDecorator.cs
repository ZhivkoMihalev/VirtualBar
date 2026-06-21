using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Feed;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class FeedValidationDecorator(FeedService inner) : IFeedService
{
    public async Task<Result<List<FeedItemDto>>> GetFeedAsync(int skip, int take, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (skip < 0) skip = 0;
        if (take < 1 || take > 100) take = 20;

        return await inner.GetFeedAsync(skip, take, cancellationToken);
    }
}
