namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// A single observed price in its native currency, before FX conversion to the base currency.
/// </summary>
/// <param name="Amount">The price amount in <paramref name="Currency"/>.</param>
/// <param name="Currency">The ISO 4217 currency code the amount is denominated in.</param>
public sealed record PricePoint(decimal Amount, string Currency);
