namespace VirtualBar.Application.Options;

/// <summary>
/// Options for the Claude (Anthropic Messages API) market-research provider and its cost guardrails
/// (slice 09). <b>Setup note:</b> web search must be enabled for the organisation in the Claude Console;
/// if the API rejects the <c>web_search</c> tool the provider fails soft (returns <c>null</c> + logs).
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>The single on/off switch for the Claude provider (binds the shared <c>UseProviderStats</c> convention).</summary>
    public bool UseProviderStats { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The Anthropic API key. <b>Secret:</b> set only in <c>appsettings.Development.json</c> or user-secrets —
    /// never committed.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model id. Default <c>claude-sonnet-4-6</c> balances cost and quality; switch to an Opus model for
    /// higher research quality, or a Haiku model for lower cost (confirm web-search support for the chosen model).
    /// </summary>
    public string Model { get; set; } = string.Empty;

    public string AnthropicVersion { get; set; } = string.Empty;

    /// <summary>Per-call cap mapped to the web_search tool's <c>max_uses</c> — bounds searches per bottle.</summary>
    public int MaxSearchesPerBottle { get; set; }

    /// <summary>
    /// The maximum number of billed Claude calls allowed per UTC day across the whole process (provider +
    /// pre-warm job). Enforced by the shared call-budget counter; when spent, research short-circuits to <c>null</c>.
    /// </summary>
    public int DailyCallBudget { get; set; }

    public string[] AllowedDomains { get; set; } = [];

    public string[] BlockedDomains { get; set; } = [];
}
