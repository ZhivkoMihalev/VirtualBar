using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Startup;

/// <summary>
/// Configuration Program.cs refuses to start with. Both checks run before the app touches the database, so these
/// tests need no SQL Server and run everywhere.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StartupGuardTests
{
    private const string UnusedConnectionString = "Server=unused;Database=unused";

    [Theory]
    [InlineData("too-short-key")]
    [InlineData("CHANGE_THIS_to_a_long_random_secret_of_at_least_32_bytes")]
    public void Startup_WhenTheJwtKeyIsWeak_Refuses(string jwtKey)
    {
        using var factory = new ApiFactory(UnusedConnectionString, new Dictionary<string, string>
        {
            ["Jwt:Key"] = jwtKey,
        });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Jwt:Key", exception.Message);
    }

    [Fact]
    public void Startup_WhenThePickupDirectoryIsSetOutsideDevelopment_Refuses()
    {
        // ApiFactory always sets a pickup directory; outside Development that alone must stop the app,
        // or nobody would get their confirmation or reset emails.
        using var factory = new ApiFactory(UnusedConnectionString, new Dictionary<string, string>(), environment: "Production");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Email:PickupDirectory", exception.Message);
    }
}
