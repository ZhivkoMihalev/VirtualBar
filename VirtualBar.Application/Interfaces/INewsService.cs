using Microsoft.AspNetCore.Http;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.News;

namespace VirtualBar.Application.Interfaces;

public interface INewsService
{
    Task<Result<List<NewsPostDto>>> GetAllAsync(int skip, int take, string lang, CancellationToken cancellationToken);

    Task<Result<NewsPostDto>> GetByIdAsync(Guid id, string lang, CancellationToken cancellationToken);

    Task<Result<NewsPostDto>> CreateAsync(CreateNewsPostRequest request, CancellationToken cancellationToken);

    Task<Result<NewsPostDto>> UpdateAsync(Guid id, UpdateNewsPostRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<string>> UploadCoverAsync(IFormFile file, string saveDirectory, CancellationToken cancellationToken);
}
