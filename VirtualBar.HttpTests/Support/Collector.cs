using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.HttpTests.Support;

/// <summary>A registered collector, with an HTTP client that sends their token on every request.</summary>
public sealed record Collector(Guid Id, string Email, HttpClient Client)
{
    /// <summary>
    /// Registers a new collector and returns them logged in. Needs a host where a new account can log in straight
    /// away (<see cref="ApiHosts.ConfirmationOff"/>), because register then issues the token.
    /// </summary>
    public static async Task<Collector> RegisterAsync(ApiFactory api, string purpose)
    {
        var client = api.CreateClient();
        var email = AuthApi.NewEmail(purpose);

        var response = await client.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        client.UseToken(body.GetProperty("token").GetString()!);
        return new Collector(body.GetProperty("user").GetProperty("id").GetGuid(), email, client);
    }

    /// <summary>
    /// Registers a new collector and makes them a platform administrator. <c>AdminEmail</c> cannot do it: it is read
    /// only at startup, before the account exists. So the flag is set in the app's own database, as the startup
    /// seeding does, and the admin logs in again, because the <c>isAdmin</c> claim is written into the token at login.
    /// </summary>
    public static async Task<Collector> RegisterAdminAsync(ApiFactory api, string purpose)
    {
        var admin = await RegisterAsync(api, purpose);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == admin.Id);
            user.IsAdmin = true;
            await db.SaveChangesAsync();
        }

        admin.Client.UseToken(await AuthApi.AssertTokenIssuedAsync(await admin.Client.LoginAsync(admin.Email)));
        return admin;
    }
}
