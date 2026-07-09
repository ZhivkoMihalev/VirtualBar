namespace VirtualBar.Application.DTOs.Pricing;

/// <summary>
/// A single citation source backing a price estimate. Anthropic mandates that citations be
/// displayed when API output is shown to end users, so every researched estimate carries these.
/// </summary>
/// <param name="Url">The source URL.</param>
/// <param name="Title">The human-readable source title.</param>
public sealed record PriceCitation(string Url, string Title);
