using System.Net;
using VirtualBar.Application.Common;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Auth;

/// <summary>
/// The confirmation email cannot be sent (#41). The account exists by then, so registration must still succeed and
/// ask for confirmation: the user recovers with "send a new link" once email works again.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConfirmationEmailFailureTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.EmailUnreachable;

    [SqlServerFact]
    public async Task Register_WhenTheEmailCannotBeSent_StillAsksForConfirmationAndKeepsTheAccount()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("smtp-down");

        var response = await client.RegisterAsync(email);

        await AuthApi.AssertConfirmationRequestedAsync(response);
        // EmailNotConfirmed, not InvalidCredentials: the account and its password were saved.
        await AuthApi.AssertErrorAsync(
            await client.LoginAsync(email), HttpStatusCode.BadRequest, AuthErrorReasons.EmailNotConfirmed);
    }

    [SqlServerFact]
    public async Task ResendConfirmation_WhenTheEmailCannotBeSent_StillReturns200()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("smtp-down-resend");
        await client.RegisterAsync(email);

        var response = await client.ResendConfirmationAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
