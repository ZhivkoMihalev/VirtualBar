using Microsoft.EntityFrameworkCore;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.HttpTests.Support;

/// <summary>
/// The app in each configuration the tests need, started on first use and shared by every test class (see
/// <see cref="ApiCollection"/>). All of them use one throwaway database: the first host's startup creates it
/// (migrations + seeders, exactly as in production) and it is dropped when the run ends. Tests keep out of each
/// other's way by registering collectors with unique emails (<see cref="AuthApi.NewEmail"/>).
/// </summary>
public sealed class ApiHosts : IAsyncLifetime
{
    // Always a name of our own: the drop at the end can never reach the development database.
    private readonly string _database = $"VirtualBar_HttpTests_{Guid.NewGuid():N}";

    private readonly List<ApiFactory> _started = [];

    private readonly Lazy<ApiFactory> _confirmationOn;

    private readonly Lazy<ApiFactory> _confirmationOff;

    private readonly Lazy<ApiFactory> _emailUnreachable;

    public ApiHosts()
    {
        _confirmationOn = new(() => Start(new Dictionary<string, string>
        {
            ["Identity:RequireConfirmedEmail"] = "true",
        }));

        _confirmationOff = new(() => Start(new Dictionary<string, string>
        {
            ["Identity:RequireConfirmedEmail"] = "false",
        }));

        // Port 1 on the loopback address refuses the connection at once, so every email fails as with an SMTP outage.
        _emailUnreachable = new(() => Start(new Dictionary<string, string>
        {
            ["Identity:RequireConfirmedEmail"] = "true",
            ["Email:PickupDirectory"] = "",
            ["Email:SmtpHost"] = "127.0.0.1",
            ["Email:SmtpPort"] = "1",
        }));
    }

    /// <summary>As in production: a new account has to confirm its email before it can log in.</summary>
    public ApiFactory ConfirmationOn => _confirmationOn.Value;

    /// <summary>As in Development and the E2E suite: a new account can log in straight away.</summary>
    public ApiFactory ConfirmationOff => _confirmationOff.Value;

    /// <summary>Confirmation on, and no email can be sent: the SMTP server refuses every connection.</summary>
    public ApiFactory EmailUnreachable => _emailUnreachable.Value;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var factory in _started)
            await factory.DisposeAsync();

        if (_started.Count > 0)
            await DropDatabaseAsync();
    }

    private ApiFactory Start(IReadOnlyDictionary<string, string> settings)
    {
        if (TestSqlServer.UnreachableReason is { } reason)
            throw new InvalidOperationException($"The HTTP tests need SQL Server: {reason}.");

        var factory = new ApiFactory(TestSqlServer.ForDatabase(_database), settings);
        _started.Add(factory);
        return factory;
    }

    private async Task DropDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(TestSqlServer.ForDatabase(_database))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}
