using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public class EmailServiceTests
{
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
}
