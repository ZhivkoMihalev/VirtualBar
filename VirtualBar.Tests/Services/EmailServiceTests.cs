using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MimeKit;
using VirtualBar.Application.Options;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class EmailServiceTests : IDisposable
{
    private readonly string _pickupDirectory = Path.Combine(Path.GetTempPath(), $"virtualbar-mail-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_pickupDirectory))
            Directory.Delete(_pickupDirectory, recursive: true);
    }

    private static EmailService CreateEmailService(string pickupDirectory) =>
        new(Options.Create(new EmailSettings
        {
            SmtpHost = "127.0.0.1",
            SmtpPort = 1,
            FromAddress = "noreply@virtualbar.test",
            FromName = "VirtualBar",
            PickupDirectory = pickupDirectory
        }));

    private async Task<MimeMessage> LoadOnlyEmailAsync()
    {
        var file = Assert.Single(Directory.GetFiles(_pickupDirectory));
        Assert.EndsWith(".eml", file);
        return await MimeMessage.LoadAsync(file, CancellationToken.None);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("bg", "bg")]
    [InlineData("en-US", "bg")]
    [InlineData("fr", "bg")]
    [InlineData(null, "bg")]
    [InlineData("", "bg")]
    public void NormalizeLanguage_WhenCalled_ReturnsExpectedLang(string? input, string expected)
    {
        var result = EmailService.NormalizeLanguage(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetSubject_WhenLangIsEn_ReturnsEnglishSubject()
    {
        var subject = EmailService.GetSubject("en");

        Assert.Equal("VirtualBar — Password Reset", subject);
    }

    [Fact]
    public void GetSubject_WhenLangIsBg_ReturnsBulgarianSubject()
    {
        var subject = EmailService.GetSubject("bg");

        Assert.Equal("VirtualBar — Нулиране на парола", subject);
    }

    [Fact]
    public void GetConfirmationSubject_WhenLangIsEn_ReturnsEnglishSubject()
    {
        var subject = EmailService.GetConfirmationSubject("en");

        Assert.Equal("VirtualBar — Confirm your email", subject);
    }

    [Fact]
    public void GetConfirmationSubject_WhenLangIsBg_ReturnsBulgarianSubject()
    {
        var subject = EmailService.GetConfirmationSubject("bg");

        Assert.Equal("VirtualBar — Потвърди имейла си", subject);
    }

    [Theory]
    [InlineData("confirm-email.bg.html")]
    [InlineData("confirm-email.en.html")]
    [InlineData("reset-password.bg.html")]
    [InlineData("reset-password.en.html")]
    public void EmailTemplate_IsEmbeddedInTheMainAssembly(string template)
    {
        // Pins the csproj's WithCulture="false": without it MSBuild treats ".bg" / ".en" as cultures and moves the
        // templates into satellite assemblies, and every email fails with "template not found".
        var names = typeof(EmailService).Assembly.GetManifestResourceNames();

        Assert.Contains($"VirtualBar.Infrastructure.EmailTemplates.{template}", names);
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_WhenPickupDirectorySet_WritesTheEmailToAFile()
    {
        var service = CreateEmailService(_pickupDirectory);
        const string link = "http://localhost:5173/confirm-email?email=new%40example.com&token=Cf%2Bz%2F1%3D";

        await service.SendEmailConfirmationAsync("new@example.com", link, "en", CancellationToken.None);

        var message = await LoadOnlyEmailAsync();
        Assert.Equal("new@example.com", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("noreply@virtualbar.test", Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal("VirtualBar — Confirm your email", message.Subject);
        Assert.Contains($"href=\"{link}\"", message.HtmlBody);
    }

    [Fact]
    public async Task SendPasswordResetAsync_WhenPickupDirectorySet_WritesTheEmailToAFile()
    {
        var service = CreateEmailService(_pickupDirectory);
        const string link = "http://localhost:5173/reset-password?email=user%40example.com&token=abc";

        await service.SendPasswordResetAsync("user@example.com", link, "bg", CancellationToken.None);

        var message = await LoadOnlyEmailAsync();
        Assert.Equal("user@example.com", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("VirtualBar — Нулиране на парола", message.Subject);
        Assert.Contains(link, message.HtmlBody);
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_WhenCalledTwice_WritesTwoFiles()
    {
        var service = CreateEmailService(_pickupDirectory);

        await service.SendEmailConfirmationAsync("a@example.com", "http://localhost/a", "bg", CancellationToken.None);
        await service.SendEmailConfirmationAsync("a@example.com", "http://localhost/b", "bg", CancellationToken.None);

        var files = Directory.GetFiles(_pickupDirectory);
        Assert.Equal(2, files.Length);
        Assert.All(files, file => Assert.EndsWith(".eml", file));
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_WhenNoPickupDirectory_SendsOverSmtp()
    {
        // A blank directory counts as none. Nothing listens on port 1, so failing to connect is what proves the
        // email went to SMTP instead of a file.
        var service = CreateEmailService("   ");

        await Assert.ThrowsAnyAsync<SocketException>(() =>
            service.SendEmailConfirmationAsync("a@example.com", "http://localhost/a", "bg", CancellationToken.None));
    }
}
