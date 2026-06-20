namespace VirtualBar.Infrastructure.Options;

public sealed class ProductLookupOptions
{
    public const string SectionName = "ProductLookup";

    public string LookupUrl { get; set; } = string.Empty;
}
