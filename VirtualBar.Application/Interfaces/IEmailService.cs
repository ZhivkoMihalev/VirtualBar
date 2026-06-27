namespace VirtualBar.Application.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string resetLink, string language, CancellationToken cancellationToken);
}
