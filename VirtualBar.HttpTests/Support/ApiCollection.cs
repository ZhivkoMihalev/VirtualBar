// Hosts built in parallel can catch each other's startup in WebApplicationFactory, and every test shares one
// database anyway, so the whole assembly runs one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VirtualBar.HttpTests.Support;

/// <summary>
/// Every HTTP test class joins this collection, so they all share one <see cref="ApiHosts"/>: one throwaway database
/// and each app configuration started once. A fresh database costs about 16 s of migrations and seeding, paid once
/// per run instead of once per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiHosts>
{
    public const string Name = "Api";
}
