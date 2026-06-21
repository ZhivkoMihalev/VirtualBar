using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class NewsService(AppDbContext db, ICurrentUser currentUser) : INewsService
{
    public async Task<Result<List<NewsPostDto>>> GetAllAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var posts = await db.NewsPosts
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(p => new NewsPostDto
            {
                Id = p.Id,
                Title = p.Title,
                Excerpt = p.Excerpt,
                Content = p.Content,
                CoverImageUrl = p.CoverImageUrl,
                AuthorId = p.AuthorId,
                AuthorDisplayName = p.Author.DisplayName,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Result<List<NewsPostDto>>.Ok(posts);
    }

    public async Task<Result<NewsPostDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await db.NewsPosts
            .Where(p => p.Id == id && !p.IsDeleted)
            .Select(p => new NewsPostDto
            {
                Id = p.Id,
                Title = p.Title,
                Excerpt = p.Excerpt,
                Content = p.Content,
                CoverImageUrl = p.CoverImageUrl,
                AuthorId = p.AuthorId,
                AuthorDisplayName = p.Author.DisplayName,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .FirstAsync(cancellationToken);

        return Result<NewsPostDto>.Ok(post);
    }

    public async Task<Result<NewsPostDto>> CreateAsync(CreateNewsPostRequest request, CancellationToken cancellationToken)
    {
        var post = new NewsPost
        {
            Title = request.Title,
            Excerpt = request.Excerpt,
            Content = request.Content,
            CoverImageUrl = request.CoverImageUrl,
            AuthorId = currentUser.UserId
        };

        db.NewsPosts.Add(post);
        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(post).Reference(p => p.Author).LoadAsync(cancellationToken);

        return Result<NewsPostDto>.Ok(Map(post));
    }

    public async Task<Result<NewsPostDto>> UpdateAsync(Guid id, UpdateNewsPostRequest request, CancellationToken cancellationToken)
    {
        var post = await db.NewsPosts
            .Include(p => p.Author)
            .FirstAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        post.Title = request.Title;
        post.Excerpt = request.Excerpt;
        post.Content = request.Content;
        post.CoverImageUrl = request.CoverImageUrl;

        await db.SaveChangesAsync(cancellationToken);

        return Result<NewsPostDto>.Ok(Map(post));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await db.NewsPosts.FindAsync([id], cancellationToken);

        post!.IsDeleted = true;
        post.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    private static NewsPostDto Map(NewsPost p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Excerpt = p.Excerpt,
        Content = p.Content,
        CoverImageUrl = p.CoverImageUrl,
        AuthorId = p.AuthorId,
        AuthorDisplayName = p.Author.DisplayName,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };
}
