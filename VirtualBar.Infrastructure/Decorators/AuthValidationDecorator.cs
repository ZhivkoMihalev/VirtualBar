using Microsoft.AspNetCore.Identity;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Auth;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class AuthValidationDecorator(
    AuthService inner,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Result<AuthResponse>.Fail("An account with this email already exists.");

        return await inner.RegisterAsync(request, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<AuthResponse>.Fail("Invalid email or password.");

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Result<AuthResponse>.Fail("Invalid email or password.");

        return await inner.LoginAsync(request, cancellationToken);
    }

    public async Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.LogoutAsync(cancellationToken);
    }

    public async Task<Result<string>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<string>.Fail("Email is required.");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<string>.Ok("If an account exists, a reset link has been sent.");

        return await inner.ForgotPasswordAsync(request, cancellationToken);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<bool>.Fail("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Token))
            return Result<bool>.Fail("Reset token is required.");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Result<bool>.Fail("New password is required.");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<bool>.Fail("Account not found.");

        return await inner.ResetPasswordAsync(request, cancellationToken);
    }
}
