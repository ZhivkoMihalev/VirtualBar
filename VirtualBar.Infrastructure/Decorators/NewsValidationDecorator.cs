using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Storage;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class NewsValidationDecorator(
    NewsService inner,
    AppDbContext db,
    ICurrentUser currentUser) : INewsService
{
    public async Task<Result<List<NewsPostDto>>> GetAllAsync(int skip, int take, string lang, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (skip < 0) skip = 0;
        if (take < 1 || take > 100) take = 20;
        if (string.IsNullOrWhiteSpace(lang)) lang = "bg";

        return await inner.GetAllAsync(skip, take, lang, cancellationToken);
    }

    public async Task<Result<NewsPostDto>> GetByIdAsync(Guid id, string lang, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (id == Guid.Empty)
            return Result<NewsPostDto>.NotFound("News post not found.");

        if (string.IsNullOrWhiteSpace(lang)) lang = "bg";

        var exists = await db.NewsPosts.AnyAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (!exists)
            return Result<NewsPostDto>.NotFound("News post not found.");

        return await inner.GetByIdAsync(id, lang, cancellationToken);
    }

    public async Task<Result<NewsPostDto>> CreateAsync(CreateNewsPostRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<NewsPostDto>.Forbidden("Only administrators can create news posts.");

        if (request.Translations.Count == 0)
            return Result<NewsPostDto>.Fail("At least one translation is required.");

        if (!request.Translations.Any(t => t.LanguageCode == "bg"))
            return Result<NewsPostDto>.Fail("Bulgarian translation is required.");

        foreach (var tr in request.Translations)
        {
            if (string.IsNullOrWhiteSpace(tr.LanguageCode))
                return Result<NewsPostDto>.Fail("Language code is required.");

            if (string.IsNullOrWhiteSpace(tr.Title))
                return Result<NewsPostDto>.Fail("Title is required.");

            if (string.IsNullOrWhiteSpace(tr.Content))
                return Result<NewsPostDto>.Fail("Content is required.");
        }

        return await inner.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<NewsPostDto>> UpdateAsync(Guid id, UpdateNewsPostRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<NewsPostDto>.Forbidden("Only administrators can update news posts.");

        if (id == Guid.Empty)
            return Result<NewsPostDto>.NotFound("News post not found.");

        var exists = await db.NewsPosts.AnyAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (!exists)
            return Result<NewsPostDto>.NotFound("News post not found.");

        if (request.Translations.Count == 0)
            return Result<NewsPostDto>.Fail("At least one translation is required.");

        foreach (var tr in request.Translations)
        {
            if (string.IsNullOrWhiteSpace(tr.LanguageCode))
                return Result<NewsPostDto>.Fail("Language code is required.");

            if (string.IsNullOrWhiteSpace(tr.Title))
                return Result<NewsPostDto>.Fail("Title is required.");

            if (string.IsNullOrWhiteSpace(tr.Content))
                return Result<NewsPostDto>.Fail("Content is required.");
        }

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

    public async Task<Result<string>> UploadCoverAsync(IFormFile file, string saveDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<string>.Forbidden("Only administrators can upload news covers.");

        if (file is null || file.Length == 0)
            return Result<string>.Fail("No file provided.");

        if (file.Length > 10 * 1024 * 1024)
            return Result<string>.Fail("File must be under 10 MB.");

        if (!ImageUploadTypes.IsAllowed(file.ContentType))
            return Result<string>.Fail($"Only {ImageUploadTypes.AllowedFormatsLabel} images are allowed.");

        return await inner.UploadCoverAsync(file, saveDirectory, cancellationToken);
    }
}
