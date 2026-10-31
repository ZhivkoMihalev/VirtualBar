using VirtualBar.Application.Common;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PriceKeyTests
{
    [Fact]
    public void ForBottle_WhenLinkedToProduct_ReturnsTheProductKeyAndIgnoresBottleFields()
    {
        var key = PriceKey.ForBottle("whisky|macallan|sherry oak|18yo|700", "Anything", "My spelling", SpiritCategory.Other, 12, 1998, 1000);

        Assert.Equal("whisky|macallan|sherry oak|18yo|700", key);
    }

    [Fact]
    public void ForBottle_WhenNotLinked_ReturnsTheBottleProductKey()
    {
        var key = PriceKey.ForBottle(null, "The Macallan", "Sherry Oak", SpiritCategory.Whisky, 18, 1998, 700);

        Assert.Equal(ProductKey.For("The Macallan", "Sherry Oak", SpiritCategory.Whisky, 18, 1998, 700), key);
        Assert.Equal("whisky|macallan|sherry oak|18yo|1998|700", key);
    }
}
