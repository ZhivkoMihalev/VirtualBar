using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Distillery;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class DistilleryService(AppDbContext db) : IDistilleryService
{
    public async Task<Result<List<DistilleryDto>>> GetAllAsync(SpiritCategory? category, CancellationToken cancellationToken)
    {
        var q = db.Distilleries
            .Where(d => !d.IsDeleted)
            .Include(d => d.Categories)
            .AsQueryable();

        if (category.HasValue)
            q = q.Where(d => d.Categories.Any(c => c.Category == category.Value));

        var distilleries = await q
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        return Result<List<DistilleryDto>>.Ok(distilleries.Select(Map).ToList());
    }

    private static DistilleryDto Map(Distillery d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Country = d.Country,
        Region = d.Region,
        Categories = d.Categories.Select(c => c.Category).ToList()
    };
}
