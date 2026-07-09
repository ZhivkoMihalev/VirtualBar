using VirtualBar.Application.Common;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class ProductKeyTests
{
    [Fact]
    public void For_WhenAllFieldsPresent_BuildsOrderedKey()
    {
        var key = ProductKey.For("The Macallan", "Sherry Oak", SpiritCategory.Whisky, 18, null, 700);

        Assert.Equal("whisky|macallan|sherry oak|18yo|700", key);
    }

    [Fact]
    public void For_WhenDistilleryNull_OmitsDistillerySegment()
    {
        var key = ProductKey.For(null, "Lagavulin 16", SpiritCategory.Whisky, null, null, null);

        Assert.Equal("whisky|lagavulin 16", key);
    }

    [Fact]
    public void For_WhenDistilleryWhitespace_OmitsDistillerySegment()
    {
        var key = ProductKey.For("   ", "Name", SpiritCategory.Rum, null, null, null);

        Assert.Equal("rum|name", key);
    }

    [Fact]
    public void For_WhenVintageAndVolumePresent_EmitsRawValues()
    {
        var key = ProductKey.For(null, "X", SpiritCategory.Rum, null, 1998, 700);

        Assert.Equal("rum|x|1998|700", key);
    }

    [Fact]
    public void For_WhenAgePresent_EmitsYoSegment()
    {
        var key = ProductKey.For(null, "X", SpiritCategory.Gin, 12, null, null);

        Assert.Equal("gin|x|12yo", key);
    }

    [Fact]
    public void For_IdenticalProductsWithDifferentCasingAndPunctuation_ProduceSameKey()
    {
        var a = ProductKey.For("Macallan!!", "The  Sherry-Oak", SpiritCategory.Whisky, 18, null, 700);
        var b = ProductKey.For("macallan", "sherry oak", SpiritCategory.Whisky, 18, null, 700);

        Assert.Equal(a, b);
    }

    [Fact]
    public void For_DropsTheNoiseWordWholeWordOnly()
    {
        // "the" is dropped as a whole word, but "theory" (which merely starts with "the") is preserved.
        Assert.Equal("whisky|macallan", ProductKey.For(null, "The Macallan", SpiritCategory.Whisky, null, null, null));
        Assert.Equal("whisky|theory", ProductKey.For(null, "Theory", SpiritCategory.Whisky, null, null, null));
    }

    [Fact]
    public void For_CollapsesWhitespaceAndStripsPunctuation()
    {
        var key = ProductKey.For(null, "  Glen   Grant's   #1  ", SpiritCategory.Whisky, null, null, null);

        Assert.Equal("whisky|glen grant s 1", key);
    }
}
