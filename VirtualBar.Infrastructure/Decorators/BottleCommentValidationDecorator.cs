using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Comments;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class BottleCommentValidationDecorator(
    BottleCommentService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IBottleCommentService
{
    public async Task<Result<List<CommentDto>>> GetCommentsAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<List<CommentDto>>.NotFound("Bottle not found.");

        return await inner.GetCommentsAsync(bottleId, cancellationToken);
    }

    public async Task<Result<CommentDto>> AddCommentAsync(Guid bottleId, AddCommentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<CommentDto>.Fail("Content is required.");

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<CommentDto>.NotFound("Bottle not found.");

        return await inner.AddCommentAsync(bottleId, request, cancellationToken);
    }

    public async Task<Result<bool>> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var comment = await db.BottleComments.FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);
        if (comment is null)
            return Result<bool>.NotFound("Comment not found.");

        if (comment.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        return await inner.DeleteCommentAsync(commentId, cancellationToken);
    }
}
