using VirtualBar.Application.Common;

namespace VirtualBar.Tests.Services;

/// <summary>The year rules shared by product requests and their approval (#59).</summary>
public sealed class ProductYearsTests
{
    [Theory]
    [InlineData("Millésime 1988", 1988, true)]
    [InlineData("1988 Vintage", 1988, true)]
    [InlineData("Millésime 19880", 1988, false)]
    [InlineData("Cask 21988", 1988, false)]
    [InlineData("Millésime", 1988, false)]
    public void InName_MatchesOnlyTheYearAsAWholeNumber(string name, int year, bool expected) =>
        Assert.Equal(expected, ProductYears.InName(name, year));

    [Fact]
    public void Problem_WhenNoYear_IsNone() =>
        Assert.Null(ProductYears.Problem("VSOP", null, null));

    [Fact]
    public void Problem_WhenBothYears_RejectsIt() =>
        Assert.Equal("A product has a vintage year or a release year, not both.",
            ProductYears.Problem("Millésime 1988", 1988, 1988));

    [Theory]
    [InlineData(1799)]
    [InlineData(3000)]
    public void Problem_WhenYearOutOfRange_RejectsIt(int year) =>
        Assert.Equal($"Year must be between 1800 and {DateTime.UtcNow.Year + 1}.",
            ProductYears.Problem($"Millésime {year}", year, null));

    [Fact]
    public void Problem_WhenReleaseYearNotInName_RejectsIt() =>
        Assert.Equal("The name must contain the year.", ProductYears.Problem("Classic Cut Edition", null, 2020));

    [Theory]
    [InlineData(1800, null)]
    [InlineData(null, 2020)]
    public void Problem_WhenOneYearInRangeAndInName_IsNone(int? vintageYear, int? releaseYear) =>
        Assert.Null(ProductYears.Problem($"Edition {vintageYear ?? releaseYear}", vintageYear, releaseYear));

    [Fact]
    public void Problem_AcceptsNextYear()
    {
        var next = DateTime.UtcNow.Year + 1;
        Assert.Null(ProductYears.Problem($"Edition {next}", null, next));
    }

    [Fact]
    public void Effective_WithoutOverride_KeepsTheRequestsYears() =>
        Assert.Equal((1988, (int?)null), ProductYears.Effective(null, null, 1988, null));

    [Theory]
    [InlineData(1990, null)]
    [InlineData(null, 2021)]
    public void Effective_WhenEitherYearIsOverridden_ReplacesBoth(int? overrideVintage, int? overrideRelease) =>
        Assert.Equal((overrideVintage, overrideRelease), ProductYears.Effective(overrideVintage, overrideRelease, 1988, null));
}
