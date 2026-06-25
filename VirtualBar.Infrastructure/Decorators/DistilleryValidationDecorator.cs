using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Distillery;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class DistilleryValidationDecorator(IDistilleryService inner) : IDistilleryService
{
    public async Task<Result<List<DistilleryDto>>> GetAllAsync(SpiritCategory? category, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetAllAsync(category, cancellationToken);
    }
}
