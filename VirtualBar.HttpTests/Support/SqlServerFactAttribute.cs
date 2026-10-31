namespace VirtualBar.HttpTests.Support;

/// <summary>
/// A test that needs SQL Server. On a machine where no server answers it is skipped, and the skip says why and how to
/// fix it. In CI it is never skipped: there it runs and fails, so a missing server can never pass as a green build.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!TestSqlServer.IsCi && TestSqlServer.UnreachableReason is { } reason)
            Skip = $"No SQL Server for the HTTP tests: {reason}. " +
                   $"Set {TestSqlServer.EnvironmentVariable} to a SQL Server connection string to run them.";
    }
}
