using System.Net;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Auth;

/// <summary>
/// reset-password with a made-up token answers the same whether or not the email has an account, so it does not
/// reveal which emails are registered (#81).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task ResetPassword_WhenTheTokenIsMadeUp_AnswersTheSameForARegisteredAndAnUnknownEmail()
    {
        var client = Api.CreateClient();
        var registered = AuthApi.NewEmail("reset");
        await AuthApi.AssertTokenIssuedAsync(await client.RegisterAsync(registered));

        var forRegistered = await client.ResetPasswordAsync(registered, "made-up-token", AuthApi.Password);
        var forUnknown = await client.ResetPasswordAsync(AuthApi.NewEmail("reset-unknown"), "made-up-token", AuthApi.Password);

        // The whole answer, byte for byte, is the one an unknown email gets.
        Assert.Equal(HttpStatusCode.BadRequest, forRegistered.StatusCode);
        Assert.Equal(forUnknown.StatusCode, forRegistered.StatusCode);
        Assert.Equal(await forUnknown.Content.ReadAsStringAsync(), await forRegistered.Content.ReadAsStringAsync());
    }
}
