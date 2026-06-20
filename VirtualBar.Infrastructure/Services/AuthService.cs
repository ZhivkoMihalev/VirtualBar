using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Auth;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;

namespace VirtualBar.Infrastructure.Services;

public sealed class AuthService(
    UserManager<AppUser> userManager,
    IConfiguration configuration) : IAuthService
{
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

        return Result<AuthResponse>.Ok(BuildAuthResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        return Result<AuthResponse>.Ok(BuildAuthResponse(user!));
    }

    public Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<bool>.Ok(true));

    public async Task<Result<string>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        await userManager.GeneratePasswordResetTokenAsync(user!);
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

    private AuthResponse BuildAuthResponse(AppUser user)
    {
        var token = GenerateJwtToken(user);
        return new AuthResponse
        {
            Token = token,
            User = new AuthUserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl,
                Country = user.Country,
                City = user.City,
                CreatedAt = user.CreatedAt
            }
        };
    }

    private string GenerateJwtToken(AppUser user)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
