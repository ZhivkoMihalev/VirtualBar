using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;

namespace VirtualBar.Infrastructure.Services;

public sealed class EmailService(IOptions<EmailSettings> options) : IEmailService
{
    private readonly EmailSettings _settings = options.Value;

    internal static string NormalizeLanguage(string? language) =>
        language == "en" ? "en" : "bg";

    internal static string GetSubject(string lang) =>
        lang == "en" ? "VirtualBar — Password Reset" : "VirtualBar — Нулиране на парола";

    public async Task SendPasswordResetAsync(string toEmail, string resetLink, string language, CancellationToken cancellationToken)
    {
        var lang = NormalizeLanguage(language);
        var resourceName = $"VirtualBar.Infrastructure.EmailTemplates.reset-password.{lang}.html";
        var assembly = typeof(EmailService).Assembly;

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var template = await reader.ReadToEndAsync(cancellationToken);

        var htmlBody = template
            .Replace("{{resetLink}}", resetLink)
            .Replace("{{year}}", DateTime.UtcNow.Year.ToString());

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = GetSubject(lang);
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
