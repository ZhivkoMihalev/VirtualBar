using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Comments;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleCommentService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService) : IBottleCommentService
{
    public async Task<Result<List<CommentDto>>> GetCommentsAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var comments = await db.BottleComments
            .Where(c => c.BottleId == bottleId && !c.IsDeleted)
            .Include(c => c.User)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<CommentDto>>.Ok(comments.Select(MapToDto).ToList());
    }

    public async Task<Result<CommentDto>> AddCommentAsync(Guid bottleId, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = new BottleComment
        {
            BottleId = bottleId,
            UserId = currentUser.UserId,
            Content = request.Content
        };

        db.BottleComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(comment).Reference(c => c.User).LoadAsync(cancellationToken);

        var bottleInfo = await db.Bottles
            .Where(b => b.Id == bottleId && !b.IsDeleted)
            .Select(b => new { b.UserId, b.Name })
            .FirstAsync(cancellationToken);

        await notificationService.CreateAsync(bottleInfo.UserId, NotificationType.BottleCommented, bottleId, bottleInfo.Name, cancellationToken);

        return Result<CommentDto>.Ok(MapToDto(comment));
    }

    public async Task<Result<bool>> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await db.BottleComments
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);

        comment!.IsDeleted = true;
        comment.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    private static CommentDto MapToDto(BottleComment c) => new()
    {
        Id = c.Id,
        BottleId = c.BottleId,
        UserId = c.UserId,
        UserDisplayName = c.User.DisplayName,
        UserAvatarUrl = c.User.AvatarUrl,
        Content = c.Content,
        CreatedAt = c.CreatedAt
    };
}
