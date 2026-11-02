namespace VirtualBar.Application.Common;

/// <summary>
/// The only currencies the app accepts for a listing or an offer (#98). Each one is the pricing base currency or has a
/// rate in <c>Pricing:FxToBase</c>, so an amount in any of them can be converted (pinned by a test).
/// </summary>
public static class SupportedCurrencies
{
    public const string NotSupportedMessage = "Currency must be EUR, GBP or USD.";

    public static IReadOnlyList<string> All { get; } = ["EUR", "GBP", "USD"];

    /// <summary>Whether the app accepts <paramref name="currency"/>, ignoring case and surrounding spaces.</summary>
    public static bool IsSupported(string? currency) =>
        currency is not null && All.Contains(ToCode(currency));

    /// <summary>The code as it is stored: trimmed and upper-case.</summary>
    public static string ToCode(string currency) => currency.Trim().ToUpperInvariant();
}
