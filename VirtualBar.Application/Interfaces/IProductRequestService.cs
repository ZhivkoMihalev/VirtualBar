using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductRequests;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Interfaces;

public interface IProductRequestService
{
    Task<Result<ProductRequestDto>> CreateAsync(CreateProductRequestRequest request, CancellationToken cancellationToken);

    Task<Result<List<ProductRequestDto>>> GetMineAsync(CancellationToken cancellationToken);

    Task<Result<bool>> WithdrawAsync(Guid requestId, CancellationToken cancellationToken);

    Task<Result<List<ProductRequestDto>>> GetAllAsync(ProductRequestStatus? status, CancellationToken cancellationToken);

    Task<Result<ProductRequestDto>> ApproveAsync(Guid requestId, ResolveProductRequestRequest request, CancellationToken cancellationToken);

    Task<Result<ProductRequestDto>> RejectAsync(Guid requestId, RejectProductRequestRequest request, CancellationToken cancellationToken);
}
