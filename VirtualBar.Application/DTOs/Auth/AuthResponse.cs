namespace VirtualBar.Application.DTOs.Auth;

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public AuthUserDto User { get; set; } = null!;
}
