using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.Messages;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class MessageServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ICurrentUser CreateCurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static IMessageService CreateMessageService(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new MessageService(db, currentUser);
        return new MessageValidationDecorator(inner, db, currentUser);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Test User")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{displayName}@example.com",
            Email = $"{displayName}@example.com",
            DisplayName = displayName
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Message SeedMessage(AppDbContext db, Guid senderId, Guid receiverId, bool isRead = false, bool isDeleted = false)
    {
        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = "Hello there.",
            IsRead = isRead,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        db.Messages.Add(message);
        db.SaveChanges();

        return message;
    }

    #region SendAsync

    [Fact]
    public async Task SendAsync_WhenContentEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());

        var result = await service.SendAsync(new SendMessageRequest { ReceiverId = Guid.NewGuid(), Content = "" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenContentWhitespace_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());

        var result = await service.SendAsync(new SendMessageRequest { ReceiverId = Guid.NewGuid(), Content = "   " }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenSelf_ReturnsFail()
    {
        var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var service = CreateMessageService(db, userId);

        var result = await service.SendAsync(new SendMessageRequest { ReceiverId = userId, Content = "Hi." }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Cannot send a message to yourself.", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenReceiverNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());

        var result = await service.SendAsync(new SendMessageRequest { ReceiverId = Guid.NewGuid(), Content = "Hi." }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Recipient not found.", result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenValid_ReturnsMessageDto()
    {
        var db = CreateDbContext();
        var sender = SeedUser(db, "Sender");
        var receiver = SeedUser(db, "Receiver");
        var service = CreateMessageService(db, sender.Id);

        var result = await service.SendAsync(new SendMessageRequest { ReceiverId = receiver.Id, Content = "Hello!" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Hello!", result.Data.Content);
        Assert.Equal(sender.Id, result.Data.SenderId);
        Assert.Equal(receiver.Id, result.Data.ReceiverId);
        Assert.Equal("Sender", result.Data.SenderDisplayName);
        Assert.False(result.Data.IsRead);
    }

    [Fact]
    public async Task SendAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.SendAsync(new SendMessageRequest { ReceiverId = Guid.NewGuid(), Content = "Hi." }, cts.Token));
    }

    #endregion

    #region GetConversationAsync

    [Fact]
    public async Task GetConversationAsync_WhenSelf_ReturnsFail()
    {
        var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var service = CreateMessageService(db, userId);

        var result = await service.GetConversationAsync(userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Cannot retrieve a conversation with yourself.", result.Error);
    }

    [Fact]
    public async Task GetConversationAsync_ReturnsMessagesInBothDirections()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        SeedMessage(db, me.Id, other.Id);
        SeedMessage(db, other.Id, me.Id);
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetConversationAsync(other.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Contains(result.Data, m => m.SenderId == me.Id && m.ReceiverId == other.Id);
        Assert.Contains(result.Data, m => m.SenderId == other.Id && m.ReceiverId == me.Id);
    }

    [Fact]
    public async Task GetConversationAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetConversationAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region MarkReadAsync

    [Fact]
    public async Task MarkReadAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());

        var result = await service.MarkReadAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Message not found.", result.Error);
    }

    [Fact]
    public async Task MarkReadAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var sender = SeedUser(db, "Sender");
        var receiver = SeedUser(db, "Receiver");
        var message = SeedMessage(db, sender.Id, receiver.Id);
        var service = CreateMessageService(db, sender.Id);

        var result = await service.MarkReadAsync(message.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the recipient can mark a message as read.", result.Error);
    }

    [Fact]
    public async Task MarkReadAsync_WhenValid_SetsIsRead()
    {
        var db = CreateDbContext();
        var sender = SeedUser(db, "Sender");
        var receiver = SeedUser(db, "Receiver");
        var message = SeedMessage(db, sender.Id, receiver.Id);
        var service = CreateMessageService(db, receiver.Id);

        var result = await service.MarkReadAsync(message.Id, CancellationToken.None);

        Assert.True(result.Success);
        var dbMessage = await db.Messages.FindAsync(message.Id);
        Assert.True(dbMessage!.IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.MarkReadAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region GetInboxAsync

    [Fact]
    public async Task GetInboxAsync_WhenNoMessages_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetInboxAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetInboxAsync_WhenHasSentMessage_ReturnsConversation()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        SeedMessage(db, me.Id, other.Id);
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetInboxAsync(CancellationToken.None);

        Assert.True(result.Success);
        var conversation = Assert.Single(result.Data!);
        Assert.Equal(other.Id, conversation.OtherUserId);
        Assert.Equal("Other", conversation.OtherUserDisplayName);
        Assert.True(conversation.LastMessageIsFromMe);
        Assert.Equal(0, conversation.UnreadCount);
    }

    [Fact]
    public async Task GetInboxAsync_WhenHasReceivedMessage_ReturnsConversation()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        SeedMessage(db, other.Id, me.Id);
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetInboxAsync(CancellationToken.None);

        Assert.True(result.Success);
        var conversation = Assert.Single(result.Data!);
        Assert.Equal(other.Id, conversation.OtherUserId);
        Assert.Equal("Other", conversation.OtherUserDisplayName);
        Assert.False(conversation.LastMessageIsFromMe);
    }

    [Fact]
    public async Task GetInboxAsync_CountsUnreadReceivedMessages()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        SeedMessage(db, other.Id, me.Id, isRead: false);
        SeedMessage(db, other.Id, me.Id, isRead: false);
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetInboxAsync(CancellationToken.None);

        Assert.True(result.Success);
        var conversation = Assert.Single(result.Data!);
        Assert.Equal(2, conversation.UnreadCount);
    }

    [Fact]
    public async Task GetInboxAsync_GroupsByOtherUser()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        SeedMessage(db, me.Id, other.Id);
        SeedMessage(db, other.Id, me.Id);
        SeedMessage(db, me.Id, other.Id);
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetInboxAsync(CancellationToken.None);

        Assert.True(result.Success);
        var conversation = Assert.Single(result.Data!);
        Assert.Equal(other.Id, conversation.OtherUserId);
    }

    [Fact]
    public async Task GetInboxAsync_ExcludesDeletedMessages()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        SeedMessage(db, me.Id, other.Id, isDeleted: true);
        var service = CreateMessageService(db, me.Id);

        var result = await service.GetInboxAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetInboxAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateMessageService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetInboxAsync(cts.Token));
    }

    #endregion
}
