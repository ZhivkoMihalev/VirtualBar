using System.Net;
using VirtualBar.Application.Common;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Auth;

/// <summary>
/// The status code and <c>{ error, reason }</c> body of every failed login and registration (#30). The frontend
/// translates the message by <c>reason</c>, so these values are API contract.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthErrorReasonTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task Login_WhenTheEmailIsUnknown_Returns400WithInvalidCredentials()
    {
        var client = Api.CreateClient();

        var response = await client.LoginAsync(AuthApi.NewEmail("unknown"));

        await AuthApi.AssertErrorAsync(response, HttpStatusCode.BadRequest, AuthErrorReasons.InvalidCredentials);
    }

    [SqlServerFact]
    public async Task Login_WhenThePasswordIsWrong_Returns400WithInvalidCredentials()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("wrong-password");
        await client.RegisterAsync(email);

        var response = await client.LoginAsync(email, "Wrong-Passw0rd");

        await AuthApi.AssertErrorAsync(response, HttpStatusCode.BadRequest, AuthErrorReasons.InvalidCredentials);
    }

    [SqlServerFact]
    public async Task Login_WhenThePasswordIsWrongForTheFifthTime_LocksTheAccount()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("lockout");
        await client.RegisterAsync(email);

        for (var attempt = 1; attempt <= 4; attempt++)
            await AuthApi.AssertErrorAsync(
                await client.LoginAsync(email, "Wrong-Passw0rd"), HttpStatusCode.BadRequest, AuthErrorReasons.InvalidCredentials);

        var fifth = await client.LoginAsync(email, "Wrong-Passw0rd");

        await AuthApi.AssertErrorAsync(fifth, HttpStatusCode.BadRequest, AuthErrorReasons.LockedOut);
        // Locked means locked: the right password is refused too until the lockout ends.
        await AuthApi.AssertErrorAsync(await client.LoginAsync(email), HttpStatusCode.BadRequest, AuthErrorReasons.LockedOut);
    }

    [SqlServerFact]
    public async Task Register_WhenTheEmailIsTaken_Returns409WithEmailTaken()
    {
        var client = Api.CreateClient();
        var email = AuthApi.NewEmail("duplicate");
        await client.RegisterAsync(email);

        var response = await client.RegisterAsync(email);

        await AuthApi.AssertErrorAsync(response, HttpStatusCode.Conflict, AuthErrorReasons.EmailTaken);
    }

    [SqlServerFact]
    public async Task Register_WhenThePasswordBreaksTheRules_Returns400WithTheFieldError()
    {
        var client = Api.CreateClient();

        var response = await client.RegisterAsync(AuthApi.NewEmail("weak-password"), "short");

        // Model validation answers before the controller runs, as a ValidationProblemDetails keyed by field.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.JsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Password", out _));
    }
}
