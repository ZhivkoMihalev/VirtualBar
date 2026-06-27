using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtualBar.Application.DTOs.Auth;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class AuthServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<AppUser> CreateUserManager(AppDbContext db)
    {
        var userStore = new UserStore<AppUser, IdentityRole<Guid>, AppDbContext, Guid>(db);
        var mockLogger = new Mock<ILogger<UserManager<AppUser>>>();
        var mockValidator = new Mock<IUserValidator<AppUser>>();
        var mockPwdValidator = new Mock<IPasswordValidator<AppUser>>();

        mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<UserManager<AppUser>>(), It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);

        mockPwdValidator
            .Setup(x => x.ValidateAsync(It.IsAny<UserManager<AppUser>>(), It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync((UserManager<AppUser> um, AppUser user, string pwd) =>
            {
                if (string.IsNullOrEmpty(pwd) || pwd.Length < 8)
                    return IdentityResult.Failed(new IdentityError { Description = "Password must be at least 8 characters long." });
                return IdentityResult.Success;
            });

        var userManager = new UserManager<AppUser>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<AppUser>(),
            new[] { mockValidator.Object },
            new[] { mockPwdValidator.Object },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            mockLogger.Object);

        // Add token provider with mock data protection
        var mockDataProtector = new Mock<IDataProtector>();
        mockDataProtector
            .Setup(x => x.Protect(It.IsAny<byte[]>()))
            .Returns<byte[]>(x => x);
        mockDataProtector
            .Setup(x => x.Unprotect(It.IsAny<byte[]>()))
            .Returns<byte[]>(x => x);

        var mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        mockDataProtectionProvider
            .Setup(x => x.CreateProtector(It.IsAny<string>()))
            .Returns(mockDataProtector.Object);

        var mockTokenLogger = new Mock<ILogger<DataProtectorTokenProvider<AppUser>>>();
        userManager.RegisterTokenProvider("Default", new DataProtectorTokenProvider<AppUser>(
            mockDataProtectionProvider.Object,
            Options.Create(new DataProtectionTokenProviderOptions()),
            mockTokenLogger.Object));

        return userManager;
    }

    private static SignInManager<AppUser> CreateSignInManager(AppDbContext db, UserManager<AppUser> userManager)
    {
        var contextAccessor = new MockHttpContextAccessor();
        var mockLogger = new Mock<ILogger<SignInManager<AppUser>>>();
        var mockUserClaimsPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();

        mockUserClaimsPrincipalFactory
            .Setup(x => x.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("sub", "1") })));

        var signInManager = new SignInManager<AppUser>(
            userManager,
            contextAccessor,
            mockUserClaimsPrincipalFactory.Object,
            Options.Create(new IdentityOptions()),
            mockLogger.Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<AppUser>>().Object);

        return signInManager;
    }

    private static IAuthService CreateAuthService(
        AppDbContext db,
        IConfiguration configuration,
        IEmailService? emailService = null)
    {
        var userManager = CreateUserManager(db);
        var signInManager = CreateSignInManager(db, userManager);

        emailService ??= CreateEmailServiceMock().Object;
        var emailOptions = Options.Create(new EmailSettings());

        var authService = new AuthService(userManager, configuration, emailService, emailOptions);
        return new AuthValidationDecorator(authService, userManager, signInManager);
    }

    private static Mock<IEmailService> CreateEmailServiceMock()
    {
        var mock = new Mock<IEmailService>();
        mock
            .Setup(e => e.SendPasswordResetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static async Task<AppUser> SeedUser(
        AppDbContext db,
        string email,
        string displayName,
        string password = "ValidPass123")
    {
        var userManager = CreateUserManager(db);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to seed user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await db.SaveChangesAsync(CancellationToken.None);
        return user;
    }

    #region RegisterAsync

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();

        await SeedUser(db, "test@example.com", "Test User");

        var authService = CreateAuthService(db, config);
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "ValidPass123",
            DisplayName = "Another User"
        };

        // Act
        var result = await authService.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("An account with this email already exists.", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordInvalid_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "short",
            DisplayName = "New User"
        };

        // Act
        var result = await authService.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_OkWithAuthResponse()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new RegisterRequest
        {
            Email = "newuser@example.com",
            Password = "ValidPass123",
            DisplayName = "New User"
        };

        // Act
        var result = await authService.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.Token);
        Assert.NotNull(result.Data.User);
        Assert.NotEqual(Guid.Empty, result.Data.User.Id);
        Assert.Equal("newuser@example.com", result.Data.User.Email);
        Assert.Equal("New User", result.Data.User.DisplayName);
    }

    #endregion

    #region LoginAsync

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "ValidPass123"
        };

        // Act
        var result = await authService.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordInvalid_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();

        await SeedUser(db, "test@example.com", "Test User", "ValidPass123");

        var authService = CreateAuthService(db, config);
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword"
        };

        // Act
        var result = await authService.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_WhenValid_OkWithAuthResponse()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();

        var user = await SeedUser(db, "test@example.com", "Test User", "ValidPass123");

        var authService = CreateAuthService(db, config);
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "ValidPass123"
        };

        // Act
        var result = await authService.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.Token);
        Assert.NotNull(result.Data.User);
        Assert.Equal(user.Id, result.Data.User.Id);
        Assert.Equal("test@example.com", result.Data.User.Email);
        Assert.Equal("Test User", result.Data.User.DisplayName);
    }

    #endregion

    #region LogoutAsync

    [Fact]
    public async Task LogoutAsync_Always_OkTrue()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        // Act
        var result = await authService.LogoutAsync(CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LogoutAsync_WithCancellationToken_OkTrue()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        var result = await authService.LogoutAsync(token);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    #endregion

    #region ForgotPasswordAsync

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailEmpty_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ForgotPasswordRequest
        {
            Email = ""
        };

        // Act
        var result = await authService.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Email is required.", result.Error);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailWhitespace_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ForgotPasswordRequest
        {
            Email = "   "
        };

        // Act
        var result = await authService.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Email is required.", result.Error);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserNotFound_OkGenericMessage()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ForgotPasswordRequest
        {
            Email = "nonexistent@example.com"
        };

        // Act
        var result = await authService.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("If an account exists, a reset link has been sent.", result.Data);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_OkGenericMessage()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();

        await SeedUser(db, "existing@example.com", "Existing User");

        var emailServiceMock = CreateEmailServiceMock();
        var authService = CreateAuthService(db, config, emailServiceMock.Object);
        var request = new ForgotPasswordRequest
        {
            Email = "existing@example.com"
        };

        // Act
        var result = await authService.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("If an account exists, a reset link has been sent.", result.Data);
        emailServiceMock.Verify(
            e => e.SendPasswordResetAsync(
                "existing@example.com",
                It.Is<string>(link => link.Contains("/reset-password?email=") && link.Contains("token=")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ResetPasswordAsync

    [Fact]
    public async Task ResetPasswordAsync_WhenEmailEmpty_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "",
            Token = "valid-token",
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Email is required.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenEmailWhitespace_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "   ",
            Token = "valid-token",
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Email is required.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenEmpty_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "",
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Reset token is required.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenWhitespace_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "   ",
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Reset token is required.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordEmpty_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "valid-token",
            NewPassword = ""
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("New password is required.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordWhitespace_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "valid-token",
            NewPassword = "   "
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("New password is required.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenUserNotFound_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        var request = new ResetPasswordRequest
        {
            Email = "nonexistent@example.com",
            Token = "valid-token",
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Account not found.", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenInvalid_Fail()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();

        await SeedUser(db, "test@example.com", "Test User", "ValidPass123");

        var authService = CreateAuthService(db, config);
        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "invalid-token",
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenValid_OkTrue()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();

        var user = await SeedUser(db, "test@example.com", "Test User", "ValidPass123");

        // Generate a valid reset token
        var userManager = CreateUserManager(db);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);

        var authService = CreateAuthService(db, config);
        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = resetToken,
            NewPassword = "NewPass456"
        };

        // Act
        var result = await authService.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task RegisterAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "ValidPass123",
            DisplayName = "Test User"
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => authService.RegisterAsync(request, cts.Token));
    }

    [Fact]
    public async Task LoginAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "ValidPass123"
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => authService.LoginAsync(request, cts.Token));
    }

    [Fact]
    public async Task LogoutAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => authService.LogoutAsync(cts.Token));
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ForgotPasswordRequest
        {
            Email = "test@example.com"
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => authService.ForgotPasswordAsync(request, cts.Token));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateTestConfiguration();
        var authService = CreateAuthService(db, config);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "token",
            NewPassword = "NewPass456"
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => authService.ResetPasswordAsync(request, cts.Token));
    }

    #endregion

    #region Helpers

    private static IConfiguration CreateTestConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            { "Jwt:Key", "ThisIsAVeryLongSecretKeyThatIsAtLeast32BytesForHS256Algorithm" },
            { "Jwt:Issuer", "VirtualBar" },
            { "Jwt:Audience", "VirtualBarClient" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    private sealed class MockHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    #endregion
}
