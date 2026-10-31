using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace VirtualBar.HttpTests.Support;

/// <summary>
/// The SQL Server the HTTP tests run on. It comes from the <c>VIRTUALBAR_TEST_SQL</c> environment variable (CI sets
/// it), or else from the API's own <c>appsettings.Development.json</c>, so a developer needs no setup of their own.
/// Only the server is taken from that connection string: the tests always work in a throwaway database of their own
/// (<see cref="ApiHosts"/>), never in the development database.
/// </summary>
internal static class TestSqlServer
{
    public const string EnvironmentVariable = "VIRTUALBAR_TEST_SQL";

    private static readonly Lazy<string?> ConnectionStringValue = new(ResolveConnectionString);

    private static readonly Lazy<string?> UnreachableReasonValue = new(Probe);

    /// <summary>GitHub Actions sets <c>CI=true</c>. There an unreachable server fails the tests instead of skipping them.</summary>
    public static bool IsCi =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Null when the server answers; otherwise why the HTTP tests cannot use it.</summary>
    public static string? UnreachableReason => UnreachableReasonValue.Value;

    /// <summary>The server's connection string, pointed at <paramref name="database"/>.</summary>
    public static string ForDatabase(string database) =>
        new SqlConnectionStringBuilder(ConnectionStringValue.Value) { InitialCatalog = database }.ConnectionString;

    private static string? ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var developmentSettings = FindApiDevelopmentSettings();
        return developmentSettings is null
            ? null
            : new ConfigurationBuilder().AddJsonFile(developmentSettings).Build().GetConnectionString("Default");
    }

    private static string? FindApiDevelopmentSettings()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "VirtualBar.Api", "appsettings.Development.json");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? Probe()
    {
        if (string.IsNullOrWhiteSpace(ConnectionStringValue.Value))
            return $"{EnvironmentVariable} is not set and VirtualBar.Api/appsettings.Development.json has no ConnectionStrings:Default";

        try
        {
            var master = new SqlConnectionStringBuilder(ConnectionStringValue.Value) { InitialCatalog = "master", ConnectTimeout = 5 };
            using var connection = new SqlConnection(master.ConnectionString);
            connection.Open();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
