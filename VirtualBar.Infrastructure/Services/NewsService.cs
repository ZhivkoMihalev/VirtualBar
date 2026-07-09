using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Storage;

namespace VirtualBar.Infrastructure.Services;

public sealed class NewsService(AppDbContext db, ICurrentUser currentUser) : INewsService
{
    public async Task<Result<List<NewsPostDto>>> GetAllAsync(int skip, int take, string lang, CancellationToken cancellationToken)
    {
        var posts = await db.NewsPosts
            .Where(p => !p.IsDeleted)
            .Include(p => p.Author)
            .Include(p => p.Translations)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result<List<NewsPostDto>>.Ok(posts.Select(p => Map(p, lang)).ToList());
    }

    public async Task<Result<NewsPostDto>> GetByIdAsync(Guid id, string lang, CancellationToken cancellationToken)
    {
        var post = await db.NewsPosts
            .Include(p => p.Author)
            .Include(p => p.Translations)
            .FirstAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        return Result<NewsPostDto>.Ok(Map(post, lang));
    }

    public async Task<Result<NewsPostDto>> CreateAsync(CreateNewsPostRequest request, CancellationToken cancellationToken)
    {
        var post = new NewsPost
        {
            CoverImageUrl = request.CoverImageUrl,
            AuthorId = currentUser.UserId,
        };

        foreach (var tr in request.Translations)
        {
            post.Translations.Add(new NewsPostTranslation
            {
                LanguageCode = tr.LanguageCode,
                Title = tr.Title,
                Content = tr.Content,
            });
        }

        db.NewsPosts.Add(post);
        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(post).Reference(p => p.Author).LoadAsync(cancellationToken);

        return Result<NewsPostDto>.Ok(Map(post, request.Translations.FirstOrDefault()?.LanguageCode ?? "bg"));
    }

    public async Task<Result<NewsPostDto>> UpdateAsync(Guid id, UpdateNewsPostRequest request, CancellationToken cancellationToken)
    {
        var post = await db.NewsPosts
            .Include(p => p.Author)
            .Include(p => p.Translations)
            .FirstAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        post.CoverImageUrl = request.CoverImageUrl;
        post.UpdatedAt = DateTime.UtcNow;

        foreach (var tr in request.Translations)
        {
            var existing = post.Translations.FirstOrDefault(t => t.LanguageCode == tr.LanguageCode);
            if (existing is not null)
            {
                existing.Title = tr.Title;
                existing.Content = tr.Content;
            }
            else
            {
                post.Translations.Add(new NewsPostTranslation
                {
                    PostId = post.Id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Content = tr.Content,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result<NewsPostDto>.Ok(Map(post, request.Translations.FirstOrDefault()?.LanguageCode ?? "bg"));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await db.NewsPosts.FindAsync([id], cancellationToken);

        post!.IsDeleted = true;
        post.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<string>> UploadCoverAsync(IFormFile file, string saveDirectory, CancellationToken cancellationToken)
    {
        // Extension is derived from the server-validated content type (never the client file name) to
        // prevent persisting an executable/markup extension under wwwroot (stored XSS). Validated in the decorator.
        ImageUploadTypes.TryGetExtension(file.ContentType, out var ext);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(saveDirectory, fileName);

        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken);

        return Result<string>.Ok($"/uploads/news/{fileName}");
    }

    private static NewsPostTranslation Resolve(NewsPost post, string lang)
    {
        return post.Translations.FirstOrDefault(t => t.LanguageCode == lang)
            ?? post.Translations.FirstOrDefault(t => t.LanguageCode == "bg")
            ?? new NewsPostTranslation { Title = string.Empty, Content = string.Empty };
    }

    private static NewsPostDto Map(NewsPost post, string lang)
    {
        var t = Resolve(post, lang);
        return new NewsPostDto
        {
            Id = post.Id,
            Title = t.Title,
            Content = t.Content,
            CoverImageUrl = post.CoverImageUrl,
            AuthorId = post.AuthorId,
            AuthorDisplayName = post.Author.DisplayName,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            Translations = post.Translations
                .Select(tr => new NewsPostTranslationDto
                {
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Content = tr.Content,
                })
                .ToList(),
        };
    }
}
