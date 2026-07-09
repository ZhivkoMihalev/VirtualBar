using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class UserProfileServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static IUserProfileService CreateService(AppDbContext db, Guid currentUserId, bool isAuthenticated = true)
    {
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns(currentUserId);
        mockUser.Setup(u => u.IsAuthenticated).Returns(isAuthenticated);
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
        mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        var inner = new UserProfileService(db, mockUser.Object, mockEnv.Object);

        return new UserProfileValidationDecorator(inner, db, mockUser.Object);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Test User")
    {
        var user = new AppUser 
        { 
            Id = Guid.NewGuid(), 
            UserName = $"{displayName}@x.com", 
            Email = $"{displayName}@x.com", 
            DisplayName = displayName 
        };

        db.Users.Add(user); 
        db.SaveChanges(); 
        
        return user;
    }

    private static void SeedFollow(AppDbContext db, Guid followerId, Guid followedId)
    {
        db.UserFollows.Add(new UserFollow { FollowerId = followerId, FollowedId = followedId });
        db.SaveChanges();
    }

    private static IFormFile CreateFormFile(string fileName = "avatar.jpg", string contentType = "image/jpeg", long length = 1024)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mock.Object;
    }

    #region GetProfileAsync

    [Fact]
    public async Task GetProfileAsync_WhenUserIdEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.GetProfileAsync(Guid.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User ID is required.", result.Error);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.GetProfileAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task GetProfileAsync_WhenNotAuthenticated_IsFollowedByMeFalse()
    {
        var db = CreateDbContext();
        var target = SeedUser(db);
        var service = CreateService(db, Guid.NewGuid(), isAuthenticated: false);

        var result = await service.GetProfileAsync(target.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Data!.IsFollowedByMe);
    }

    [Fact]
    public async Task GetProfileAsync_WhenAuthenticatedAndFollowing_IsFollowedByMeTrue()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var target = SeedUser(db, "Target");
        SeedFollow(db, me.Id, target.Id);
        var service = CreateService(db, me.Id);

        var result = await service.GetProfileAsync(target.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsFollowedByMe);
    }

    [Fact]
    public async Task GetProfileAsync_WhenAuthenticatedAndNotFollowing_IsFollowedByMeFalse()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var target = SeedUser(db, "Target");
        var service = CreateService(db, me.Id);

        var result = await service.GetProfileAsync(target.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Data!.IsFollowedByMe);
    }

    [Fact]
    public async Task GetProfileAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetProfileAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region SearchUsersAsync

    [Fact]
    public async Task SearchUsersAsync_WhenQueryNull_ReturnsAllUsers()
    {
        var db = CreateDbContext();
        SeedUser(db, "Alice");
        SeedUser(db, "Bob");
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.SearchUsersAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task SearchUsersAsync_WhenQueryWhitespace_ReturnsAllUsers()
    {
        var db = CreateDbContext();
        SeedUser(db, "Alice");
        SeedUser(db, "Bob");
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.SearchUsersAsync("   ", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task SearchUsersAsync_WhenQueryNonEmpty_FiltersByDisplayName()
    {
        var db = CreateDbContext();
        SeedUser(db, "Alice");
        SeedUser(db, "Bob");
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.SearchUsersAsync("Ali", CancellationToken.None);

        Assert.True(result.Success);
        var single = Assert.Single(result.Data!);
        Assert.Equal("Alice", single.DisplayName);
    }

    [Fact]
    public async Task SearchUsersAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.SearchUsersAsync(null, cts.Token));
    }

    #endregion

    #region UpdateProfileAsync

    [Fact]
    public async Task UpdateProfileAsync_WhenDisplayNameWhitespace_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateProfileRequest { DisplayName = "   " };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Display name is required.", result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenDisplayNameTooShort_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateProfileRequest { DisplayName = "A" };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Display name must be at least 2 characters.", result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenDisplayNameTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateProfileRequest { DisplayName = new string('A', 101) };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Display name must be at most 100 characters.", result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenBioTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateProfileRequest { DisplayName = "Valid", Bio = new string('B', 501) };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bio must be at most 500 characters.", result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateProfileRequest { DisplayName = "Valid" };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenBioNull_SetsBioNull()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new UpdateProfileRequest { DisplayName = "Updated", Bio = null };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Bio);
        var reloaded = await db.Users.FindAsync(user.Id);
        Assert.Null(reloaded!.Bio);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenBioWhitespace_SetsBioNull()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new UpdateProfileRequest { DisplayName = "Updated", Bio = "   " };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Bio);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenAllFieldsSet_TrimsAndPersists()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new UpdateProfileRequest
        {
            DisplayName = "  New Name  ",
            Bio = "  My bio  ",
            Country = "  Bulgaria  ",
            City = "  Sofia  "
        };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New Name", result.Data!.DisplayName);
        Assert.Equal("My bio", result.Data.Bio);
        Assert.Equal("Bulgaria", result.Data.Country);
        Assert.Equal("Sofia", result.Data.City);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenCountryAndCityNull_SetsThemNull()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new UpdateProfileRequest { DisplayName = "Updated", Country = null, City = null };

        var result = await service.UpdateProfileAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Country);
        Assert.Null(result.Data.City);
    }

    [Fact]
    public async Task UpdateProfileAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateProfileRequest { DisplayName = "Valid" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UpdateProfileAsync(request, cts.Token));
    }

    #endregion

    #region UploadAvatarAsync

    [Fact]
    public async Task UploadAvatarAsync_WhenFileEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UploadAvatarAsync(CreateFormFile(length: 0), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("File is empty.", result.Error);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenFileTooLarge_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UploadAvatarAsync(CreateFormFile(length: 6 * 1024 * 1024), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("File size must not exceed 5 MB.", result.Error);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenWrongContentType_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UploadAvatarAsync(CreateFormFile(contentType: "application/pdf"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only JPEG, PNG and WebP images are allowed.", result.Error);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UploadAvatarAsync(CreateFormFile(length: 100), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenValid_ReturnsAvatarUrl()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.UploadAvatarAsync(CreateFormFile(length: 100), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data!.AvatarUrl);
        Assert.StartsWith("/uploads/avatars/", result.Data.AvatarUrl);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenWebRootPathNull_FallsBackToContentRoot()
    {
        // If wwwroot is absent at startup, WebRootPath is null; the upload must fall back to
        // ContentRootPath/wwwroot rather than NRE into a 500.
        var db = CreateDbContext();
        var user = SeedUser(db);
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns(user.Id);
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns((string)null!);
        mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        var service = new UserProfileValidationDecorator(
            new UserProfileService(db, mockUser.Object, mockEnv.Object), db, mockUser.Object);

        var result = await service.UploadAvatarAsync(CreateFormFile(length: 100), CancellationToken.None);

        Assert.True(result.Success);
        Assert.StartsWith("/uploads/avatars/", result.Data!.AvatarUrl);
    }

    [Fact]
    public async Task UploadAvatarAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UploadAvatarAsync(CreateFormFile(), cts.Token));
    }

    #endregion
}
