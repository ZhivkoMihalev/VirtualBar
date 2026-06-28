using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Auth;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class AuthValidationDecorator(
    AuthService inner,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ICurrentUser currentUser,
    ILogger<AuthValidationDecorator> logger) : IAuthService
{
    private const string GenericResetMessage = "If an account exists, a reset link has been sent.";

    private const string GenericConfirmationMessage = "If an account exists, a confirmation link has been sent.";

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            if (userManager.Options.SignIn.RequireConfirmedEmail)
                return Result<AuthResponse>.Ok(new AuthResponse { RequiresEmailConfirmation = true });

            return Result<AuthResponse>.Conflict("An account with this email already exists.");
        }

        return await inner.RegisterAsync(request, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<AuthResponse>.Fail("Invalid email or password.");

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Result<AuthResponse>.Fail("This account is temporarily locked due to too many failed attempts. Please try again later.");
        if (result.IsNotAllowed)
            return Result<AuthResponse>.Fail("Please confirm your email address before signing in.");
        if (!result.Succeeded)
            return Result<AuthResponse>.Fail("Invalid email or password.");

        return inner.IssueTokenFor(user);
    }

    public async Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(currentUser.UserId.ToString());
        if (user is not null)
            await userManager.UpdateSecurityStampAsync(user);

        return await inner.LogoutAsync(cancellationToken);
    }

    public async Task<Result<bool>> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<bool>.Fail("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Token))
            return Result<bool>.Fail("Confirmation token is required.");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<bool>.Fail("Invalid or expired confirmation link.");

        return await inner.ConfirmEmailAsync(request, cancellationToken);
    }

    public async Task<Result<string>> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<string>.Fail("Email is required.");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.EmailConfirmed)
            return Result<string>.Ok(GenericConfirmationMessage);

        try
        {
            return await inner.ResendConfirmationAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send confirmation email.");
            return Result<string>.Ok(GenericConfirmationMessage);
        }
    }

    public async Task<Result<string>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<string>.Fail("Email is required.");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<string>.Ok(GenericResetMessage);

        try
        {
            return await inner.ForgotPasswordAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email.");
            return Result<string>.Ok(GenericResetMessage);
        }
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
            return Result<bool>.Fail("Invalid or expired reset link.");

        return await inner.ResetPasswordAsync(request, cancellationToken);
    }
}
