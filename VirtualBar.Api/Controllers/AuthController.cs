using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Auth;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Registers a new collector account. Returns a JWT, or a confirmation prompt when email verification is required.</summary>
    /// <param name="request">Email, password and display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Account created; returns a token, or a flag indicating an email confirmation was sent.</response>
    /// <response code="400">Password does not meet requirements.</response>
    /// <response code="409">Email already in use.</response>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Authenticates a collector and returns a JWT.</summary>
    /// <param name="request">Email and password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Authenticated; returns token and user info.</response>
    /// <response code="400">Invalid credentials, locked account, or unconfirmed email.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Logs the current user out and revokes their existing tokens by rotating the security stamp.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Logout acknowledged.</response>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }

    /// <summary>Confirms a user's email address using a confirmation token.</summary>
    /// <param name="request">Email and confirmation token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Email confirmed.</response>
    /// <response code="400">Invalid or expired confirmation link.</response>
    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ConfirmEmailAsync(request, cancellationToken);
        return result.Success
            ? Ok(new { message = "Email confirmed successfully." })
            : result.ToActionResult(this);
    }

    /// <summary>Resends the email confirmation link. Always returns a generic message to avoid account enumeration.</summary>
    /// <param name="request">The account email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">A confirmation link has been sent if the account exists and is unconfirmed.</response>
    /// <response code="400">The request could not be processed.</response>
    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ResendConfirmationAsync(request, cancellationToken);
        return result.Success
            ? Ok(new { message = result.Data })
            : result.ToActionResult(this);
    }

    /// <summary>Initiates a password reset for the given email. Always returns a generic message to avoid account enumeration.</summary>
    /// <param name="request">The account email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">A reset link has been sent if the account exists.</response>
    /// <response code="400">The request could not be processed.</response>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ForgotPasswordAsync(request, cancellationToken);
        return result.Success
            ? Ok(new { message = result.Data })
            : result.ToActionResult(this);
    }

    /// <summary>Resets a user's password using a reset token.</summary>
    /// <param name="request">Email, reset token, and new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Password reset successfully.</response>
    /// <response code="400">Invalid token, email, or password does not meet requirements.</response>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ResetPasswordAsync(request, cancellationToken);
        return result.Success
            ? Ok(new { message = "Password reset successfully." })
            : result.ToActionResult(this);
    }
}
