namespace VirtualBar.Application.Options;

public sealed class EmailSettings
{
    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = string.Empty;

    public string FrontendBaseUrl { get; init; } = string.Empty;
}
