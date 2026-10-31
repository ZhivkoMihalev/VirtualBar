namespace VirtualBar.HttpTests.Support;

/// <summary>A data-driven test that needs SQL Server, skipped exactly like <see cref="SqlServerFactAttribute"/>.</summary>
public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute() => Skip = TestSqlServer.SkipReason;
}
