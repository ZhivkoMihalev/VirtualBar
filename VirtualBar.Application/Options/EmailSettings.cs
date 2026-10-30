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

    /// <summary>
    /// Development and E2E only. When set, emails are written to this directory as .eml files instead of being sent:
    /// a developer can open them, and the E2E tests follow the links in them. Startup refuses it outside Development.
    /// </summary>
    public string PickupDirectory { get; init; } = string.Empty;
}
