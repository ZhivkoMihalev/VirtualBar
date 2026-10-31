using System.Net;
using VirtualBar.Application.Common;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Auth;

/// <summary>
/// Register → email → confirm → log in, with confirmation on as in production (#33). The E2E suite runs with it
/// off, so this is the only place the real sequence runs against the real API.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmailConfirmationTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOn;

    [SqlServerFact]
    public async Task Register_WhenConfirmationIsRequired_IssuesNoTokenAndSendsTheConfirmationEmail()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("register");

        var response = await client.RegisterAsync(email);

        await AuthApi.AssertConfirmationRequestedAsync(response);
        var sent = Assert.Single(Api.Mailbox.To(email));
        Assert.Equal(email, PickupMailbox.ConfirmationLink(sent).Email);
    }

    [SqlServerFact]
    public async Task Login_WhenEmailIsNotConfirmed_Returns400WithEmailNotConfirmed()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("unconfirmed");
        await client.RegisterAsync(email);

        var response = await client.LoginAsync(email);

        await AuthApi.AssertErrorAsync(response, HttpStatusCode.BadRequest, AuthErrorReasons.EmailNotConfirmed);
    }

    [SqlServerFact]
    public async Task ConfirmEmail_WhenTheLinkComesFromTheEmail_LetsTheAccountLogIn()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("confirm");
        await client.RegisterAsync(email);
        var link = PickupMailbox.ConfirmationLink(Assert.Single(Api.Mailbox.To(email)));

        var confirm = await client.ConfirmEmailAsync(link.Email, link.Token);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        await AuthApi.AssertTokenIssuedAsync(await client.LoginAsync(email));
    }

    [SqlServerFact]
    public async Task ConfirmEmail_WhenTheTokenIsWrong_Returns400AndTheAccountStaysUnconfirmed()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("wrong-token");
        await client.RegisterAsync(email);

        var confirm = await client.ConfirmEmailAsync(email, "not-a-real-token");

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        await AuthApi.AssertErrorAsync(
            await client.LoginAsync(email), HttpStatusCode.BadRequest, AuthErrorReasons.EmailNotConfirmed);
    }

    [SqlServerFact]
    public async Task ResendConfirmation_WhenTheAccountIsUnconfirmed_SendsANewLinkThatWorks()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("resend");
        await client.RegisterAsync(email);

        var resend = await client.ResendConfirmationAsync(email);

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var emails = Api.Mailbox.To(email);
        Assert.Equal(2, emails.Count);
        var link = PickupMailbox.ConfirmationLink(emails[^1]);
        Assert.Equal(HttpStatusCode.OK, (await client.ConfirmEmailAsync(link.Email, link.Token)).StatusCode);
        await AuthApi.AssertTokenIssuedAsync(await client.LoginAsync(email));
    }

    [SqlServerFact]
    public async Task Register_WhenTheEmailAlreadyHasAnAccount_AnswersAsForANewAccountAndSendsNoEmail()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("taken");
        var first = await client.RegisterAsync(email);

        var second = await client.RegisterAsync(email);

        // The whole answer, byte for byte, is the one the new account got, so it does not reveal that the email is taken.
        await AuthApi.AssertConfirmationRequestedAsync(second);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Single(Api.Mailbox.To(email));
    }
}
