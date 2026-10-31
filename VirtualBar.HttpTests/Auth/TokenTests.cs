using System.Net;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Auth;

/// <summary>
/// The JWT setup in Program.cs: a protected endpoint needs a token, accepts the one the API issued, and refuses it
/// after logout, which rotates the security stamp the token carries.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TokenTests(ApiHosts hosts)
{
    private const string ProtectedEndpoint = "/api/notifications";

    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task ProtectedEndpoint_WhenThereIsNoToken_Returns401()
    {
        var client = Api.CreateClient();

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SqlServerFact]
    public async Task ProtectedEndpoint_WhenTheTokenComesFromRegister_Returns200()
    {
        var client = Api.CreateClient();
        client.UseToken(await AuthApi.AssertTokenIssuedAsync(await client.RegisterAsync(AuthApi.NewEmail("token"))));

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Logout_WhenTheTokenIsUsedAgain_Returns401()
    {
        var client = Api.CreateClient();
        client.UseToken(await AuthApi.AssertTokenIssuedAsync(await client.RegisterAsync(AuthApi.NewEmail("logout"))));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ProtectedEndpoint)).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ProtectedEndpoint)).StatusCode);
    }
}
