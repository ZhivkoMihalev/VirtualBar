using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class NewsValidationDecorator(
    NewsService inner,
    AppDbContext db,
    ICurrentUser currentUser) : INewsService
{
    public async Task<Result<List<NewsPostDto>>> GetAllAsync(int skip, int take, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (skip < 0) skip = 0;
        if (take < 1 || take > 100) take = 20;

        return await inner.GetAllAsync(skip, take, cancellationToken);
    }

    public async Task<Result<NewsPostDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (id == Guid.Empty)
            return Result<NewsPostDto>.Fail("Post ID is required.");

        var exists = await db.NewsPosts.AnyAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (!exists)
            return Result<NewsPostDto>.NotFound("News post not found.");

        return await inner.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<NewsPostDto>> CreateAsync(CreateNewsPostRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<NewsPostDto>.Forbidden("Only administrators can create news posts.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<NewsPostDto>.Fail("Title is required.");

        if (string.IsNullOrWhiteSpace(request.Excerpt))
            return Result<NewsPostDto>.Fail("Excerpt is required.");

        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<NewsPostDto>.Fail("Content is required.");

        return await inner.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<NewsPostDto>> UpdateAsync(Guid id, UpdateNewsPostRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<NewsPostDto>.Forbidden("Only administrators can update news posts.");

        if (id == Guid.Empty)
            return Result<NewsPostDto>.Fail("Post ID is required.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<NewsPostDto>.Fail("Title is required.");

        if (string.IsNullOrWhiteSpace(request.Excerpt))
            return Result<NewsPostDto>.Fail("Excerpt is required.");

        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<NewsPostDto>.Fail("Content is required.");

        var exists = await db.NewsPosts.AnyAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (!exists)
            return Result<NewsPostDto>.NotFound("News post not found.");

        return await inner.UpdateAsync(id, request, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<bool>.Forbidden("Only administrators can delete news posts.");

        if (id == Guid.Empty)
            return Result<bool>.Fail("Post ID is required.");

        var exists = await db.NewsPosts.AnyAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (!exists)
            return Result<bool>.NotFound("News post not found.");

        return await inner.DeleteAsync(id, cancellationToken);
    }
}
