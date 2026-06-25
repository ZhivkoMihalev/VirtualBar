using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Distillery;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Interfaces;

public interface IDistilleryService
{
    Task<Result<List<DistilleryDto>>> GetAllAsync(SpiritCategory? category, CancellationToken cancellationToken);
}
