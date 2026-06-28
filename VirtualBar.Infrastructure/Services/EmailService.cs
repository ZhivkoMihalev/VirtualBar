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

    internal static string GetConfirmationSubject(string lang) =>
        lang == "en" ? "VirtualBar — Confirm your email" : "VirtualBar — Потвърди имейла си";

    public async Task SendPasswordResetAsync(string toEmail, string resetLink, string language, CancellationToken cancellationToken)
    {
        var lang = NormalizeLanguage(language);
        var htmlBody = await RenderTemplateAsync(
            $"VirtualBar.Infrastructure.EmailTemplates.reset-password.{lang}.html",
            ("{{resetLink}}", resetLink),
            cancellationToken);

        await SendAsync(toEmail, GetSubject(lang), htmlBody, cancellationToken);
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, string language, CancellationToken cancellationToken)
    {
        var lang = NormalizeLanguage(language);
        var htmlBody = await RenderTemplateAsync(
            $"VirtualBar.Infrastructure.EmailTemplates.confirm-email.{lang}.html",
            ("{{confirmationLink}}", confirmationLink),
            cancellationToken);

        await SendAsync(toEmail, GetConfirmationSubject(lang), htmlBody, cancellationToken);
    }

    private static async Task<string> RenderTemplateAsync(
        string resourceName,
        (string Placeholder, string Value) link,
        CancellationToken cancellationToken)
    {
        var assembly = typeof(EmailService).Assembly;

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var template = await reader.ReadToEndAsync(cancellationToken);

        return template
            .Replace(link.Placeholder, link.Value)
            .Replace("{{year}}", DateTime.UtcNow.Year.ToString());
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
