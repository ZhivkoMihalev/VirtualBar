using Microsoft.Extensions.Configuration;
using VirtualBar.Application.Common;
using VirtualBar.Application.Options;

namespace VirtualBar.Tests.Services;

/// <summary>The currencies the app accepts (#98): EUR, GBP and USD, and nothing else.</summary>
public sealed class SupportedCurrenciesTests
{
    [Fact]
    public void All_IsEurGbpAndUsd()
    {
        Assert.Equal(["EUR", "GBP", "USD"], SupportedCurrencies.All);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("eur")]
    [InlineData(" Usd ")]
    public void IsSupported_WhenEurGbpOrUsdInAnyCase_ReturnsTrue(string currency)
    {
        Assert.True(SupportedCurrencies.IsSupported(currency));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("BGN")]
    [InlineData("CHF")]
    [InlineData("JPY")]
    [InlineData("CAD")]
    [InlineData("AUD")]
    [InlineData("EURO")]
    public void IsSupported_WhenAnyOtherValue_ReturnsFalse(string? currency)
    {
        Assert.False(SupportedCurrencies.IsSupported(currency));
    }

    [Fact]
    public void ToCode_WhenGivenSpacesAndLowerCase_ReturnsTheTrimmedUpperCaseCode()
    {
        Assert.Equal("GBP", SupportedCurrencies.ToCode(" gbp "));
    }

    /// <summary>
    /// Pricing converts every amount to the base currency through <c>Pricing:FxToBase</c>, so each supported currency
    /// must be the base currency or have a rate there. Reads the API's real <c>appsettings.json</c>.
    /// </summary>
    [Fact]
    public void All_EveryCurrencyCanBeConvertedByThePricingFxTable()
    {
        var pricing = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build()
            .GetSection("Pricing")
            .Get<PricingOptions>()!;

        foreach (var currency in SupportedCurrencies.All)
        {
            var isBase = string.Equals(currency, pricing.BaseCurrency, StringComparison.OrdinalIgnoreCase);
            var hasRate = pricing.FxToBase.TryGetValue(currency, out var rate) && rate > 0;
            Assert.True(isBase || hasRate, $"{currency} is neither the base currency nor in Pricing:FxToBase.");
        }
    }
}
