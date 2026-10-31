using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace VirtualBar.HttpTests.Support;

/// <summary>
/// The whole API in memory: the real Program.cs with its middleware, authentication, DI and startup, on SQL Server.
/// Every setting goes in through <c>UseSetting</c>, because Program.cs reads some of them (<c>Jwt:Key</c>,
/// <c>Email:PickupDirectory</c>, <c>Identity:RequireConfirmedEmail</c>) before <c>Build()</c>, and only
/// <c>UseSetting</c> reaches the app that early. They override appsettings*.json and user secrets.
/// </summary>
public sealed class ApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string> settings,
    string environment = "Development") : WebApplicationFactory<Program>
{
    /// <summary>Where the API writes every email it sends (<c>Email:PickupDirectory</c>).</summary>
    public PickupMailbox Mailbox { get; } = PickupMailbox.CreateTemporary();

    /// <summary>
    /// The app's web root (<c>webroot</c>), a temporary folder: every upload is written here and served from here, so
    /// the tests never write into <c>VirtualBar.Api/wwwroot</c>.
    /// </summary>
    public string WebRoot { get; } =
        Path.Combine(Path.GetTempPath(), "virtualbar-httptests", $"webroot-{Guid.NewGuid():N}");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Mailbox.Delete();

        if (Directory.Exists(WebRoot))
            Directory.Delete(WebRoot, recursive: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The host serves static files only from a web root that exists when it starts.
        Directory.CreateDirectory(WebRoot);

        builder.UseEnvironment(environment);

        foreach (var (key, value) in DefaultSettings())
            builder.UseSetting(key, value);

        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);
    }

    // The same overrides the E2E workflow gives the API: nothing leaves the machine (no emails, no Claude calls,
    // no background price research) and the auth rate limit does not throttle a suite that runs from one address.
    // On top of those, uploads go to a temporary web root instead of the API project's wwwroot.
    private Dictionary<string, string> DefaultSettings() => new()
    {
        ["ConnectionStrings:Default"] = connectionString,
        [WebHostDefaults.WebRootKey] = WebRoot,
        ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
        ["Email:PickupDirectory"] = Mailbox.Folder,
        ["AdminEmail"] = "",
        ["RateLimiting:AuthPermitLimit"] = "10000",
        ["Anthropic:UseProviderStats"] = "false",
        ["Pricing:RefreshEnabled"] = "false",
        ["Serilog:MinimumLevel:Default"] = "Warning",
    };
}
