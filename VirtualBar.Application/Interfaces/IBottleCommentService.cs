using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Comments;

namespace VirtualBar.Application.Interfaces;

public interface IBottleCommentService
{
    Task<Result<List<CommentDto>>> GetCommentsAsync(Guid bottleId, CancellationToken cancellationToken);

    Task<Result<CommentDto>> AddCommentAsync(Guid bottleId, AddCommentRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken);
}
