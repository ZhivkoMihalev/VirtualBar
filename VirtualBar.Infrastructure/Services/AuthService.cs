using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Auth;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;

namespace VirtualBar.Infrastructure.Services;

public sealed class AuthService(
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    IEmailService emailService,
    IOptions<EmailSettings> emailOptions) : IAuthService
{
    public const string SecurityStampClaim = "securityStamp";

    private const int DefaultAccessTokenLifetimeMinutes = 1440;

    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Result<AuthResponse>.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        if (userManager.Options.SignIn.RequireConfirmedEmail)
        {
            await SendConfirmationEmailAsync(user, request.Language, cancellationToken);
            return Result<AuthResponse>.Ok(new AuthResponse
            {
                RequiresEmailConfirmation = true,
                User = MapUser(user)
            });
        }

        return Result<AuthResponse>.Ok(BuildAuthResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        return IssueTokenFor(user!);
    }

    public Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<bool>.Ok(true));

    public async Task<Result<bool>> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        var result = await userManager.ConfirmEmailAsync(user!, request.Token);
        if (!result.Succeeded)
            return Result<bool>.Fail("Invalid or expired confirmation link.");

        return Result<bool>.Ok(true);
    }

    public async Task<Result<string>> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        await SendConfirmationEmailAsync(user!, request.Language, cancellationToken);
        return Result<string>.Ok("If an account exists, a confirmation link has been sent.");
    }

    public async Task<Result<string>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        var token = await userManager.GeneratePasswordResetTokenAsync(user!);
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(request.Email);
        var resetLink = $"{_emailSettings.FrontendBaseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
        await emailService.SendPasswordResetAsync(request.Email, resetLink, request.Language ?? "bg", cancellationToken);
        return Result<string>.Ok("If an account exists, a reset link has been sent.");
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        var result = await userManager.ResetPasswordAsync(user!, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return Result<bool>.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        return Result<bool>.Ok(true);
    }

    public Result<AuthResponse> IssueTokenFor(AppUser user) =>
        Result<AuthResponse>.Ok(BuildAuthResponse(user));

    private async Task SendConfirmationEmailAsync(AppUser user, string? language, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(user.Email!);
        var confirmationLink = $"{_emailSettings.FrontendBaseUrl}/confirm-email?email={encodedEmail}&token={encodedToken}";
        await emailService.SendEmailConfirmationAsync(user.Email!, confirmationLink, language ?? "bg", cancellationToken);
    }

    private AuthResponse BuildAuthResponse(AppUser user) =>
        new()
        {
            Token = GenerateJwtToken(user),
            User = MapUser(user)
        };

    private static AuthUserDto MapUser(AppUser user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            AvatarUrl = user.AvatarUrl,
            Country = user.Country,
            City = user.City,
            IsAdmin = user.IsAdmin,
            CreatedAt = user.CreatedAt
        };

    private string GenerateJwtToken(AppUser user)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var lifetimeMinutes = configuration.GetValue<int?>("Jwt:AccessTokenLifetimeMinutes")
            ?? DefaultAccessTokenLifetimeMinutes;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("isAdmin", user.IsAdmin.ToString().ToLowerInvariant()),
            new(SecurityStampClaim, user.SecurityStamp ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
