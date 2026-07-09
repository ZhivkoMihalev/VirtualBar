namespace VirtualBar.Application.Options;

public sealed class InternalProviderOptions
{
    public const string SectionName = "InternalProvider";

    public bool UseProviderStats { get; set; }

    public int MinSamples { get; set; }

    public int MinApproxSamples { get; set; }
}
