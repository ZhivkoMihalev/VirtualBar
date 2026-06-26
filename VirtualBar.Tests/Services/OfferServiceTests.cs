using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.Offers;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class OfferServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ICurrentUser CreateCurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static IOfferService CreateOfferService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new OfferService(db, currentUser, notificationService ?? Mock.Of<INotificationService>());
        return new OfferValidationDecorator(inner, db, currentUser);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Test User")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{displayName}@example.com",
            Email = $"{displayName}@example.com",
            DisplayName = displayName
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Bottle SeedBottle(
        AppDbContext db,
        Guid userId,
        string name = "Lagavulin 16",
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static Offer SeedOffer(
        AppDbContext db,
        Guid bottleId,
        Guid buyerId,
        Guid sellerId,
        OfferStatus status = OfferStatus.Pending,
        decimal offeredPrice = 100m,
        string currency = "USD",
        string? message = null,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var offer = new Offer
        {
            BottleId = bottleId,
            BuyerId = buyerId,
            SellerId = sellerId,
            OfferedPrice = offeredPrice,
            Currency = currency,
            Message = message,
            Status = status,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Offers.Add(offer);
        db.SaveChanges();
        return offer;
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WhenPriceNotPositive_ReturnsFail()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = Guid.NewGuid(), OfferedPrice = 0m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offered price must be greater than zero.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenCurrencyEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = Guid.NewGuid(), OfferedPrice = 50m, Currency = "  " };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Currency is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = Guid.NewGuid(), OfferedPrice = 50m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenBottleSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, isDeleted: true);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 50m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenBuyerIsSeller_ReturnsFail()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateOfferService(db, owner.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 50m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("You cannot make an offer on your own bottle.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenExistingPendingOffer_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Pending);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 75m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("You already have a pending offer on this bottle.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenPreviousOfferNotPending_AllowsNewOffer()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Declined);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 75m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_SavesOfferAndNotifiesSeller()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, name: "Macallan 18");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, buyer.Id, notificationMock.Object);
        var request = new CreateOfferRequest
        {
            BottleId = bottle.Id,
            OfferedPrice = 250m,
            Currency = "EUR",
            Message = "Interested!"
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(bottle.Id, result.Data.BottleId);
        Assert.Equal("Macallan 18", result.Data.BottleName);
        Assert.Equal(buyer.Id, result.Data.BuyerId);
        Assert.Equal("Buyer", result.Data.BuyerDisplayName);
        Assert.Equal(seller.Id, result.Data.SellerId);
        Assert.Equal("Seller", result.Data.SellerDisplayName);
        Assert.Equal(250m, result.Data.OfferedPrice);
        Assert.Equal("EUR", result.Data.Currency);
        Assert.Equal("Interested!", result.Data.Message);
        Assert.Equal(OfferStatus.Pending, result.Data.Status);

        var stored = await db.Offers.SingleAsync();
        Assert.Equal(buyer.Id, stored.BuyerId);
        Assert.Equal(seller.Id, stored.SellerId);

        notificationMock.Verify(n => n.CreateAsync(
            seller.Id, NotificationType.OfferReceived, stored.Id, "Macallan 18", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenBuyerMissing_UsesEmptyBuyerDisplayName()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var bottle = SeedBottle(db, seller.Id);
        var orphanBuyerId = Guid.NewGuid();
        var service = CreateOfferService(db, orphanBuyerId);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 60m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("", result.Data!.BuyerDisplayName);
    }

    [Fact]
    public async Task CreateAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.CreateAsync(new CreateOfferRequest { OfferedPrice = 10m, Currency = "USD" }, cts.Token));
    }

    #endregion

    #region GetReceivedAsync

    [Fact]
    public async Task GetReceivedAsync_WhenNoOffers_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetReceivedAsync_WhenHasOffers_ReturnsOnlyOffersWhereCurrentUserIsSeller()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottleOwned = SeedBottle(db, seller.Id, "Owned");
        var bottleOther = SeedBottle(db, buyer.Id, "Other");
        var received = SeedOffer(db, bottleOwned.Id, buyer.Id, seller.Id);
        SeedOffer(db, bottleOther.Id, seller.Id, buyer.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(received.Id, result.Data![0].Id);
        Assert.Equal("Owned", result.Data![0].BottleName);
        Assert.Equal("Buyer", result.Data![0].BuyerDisplayName);
        Assert.Equal("Seller", result.Data![0].SellerDisplayName);
    }

    [Fact]
    public async Task GetReceivedAsync_WhenOfferSoftDeleted_ExcludesDeleted()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, isDeleted: true);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetReceivedAsync_WhenMultipleOffers_OrderedByCreatedAtDescending()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, offeredPrice: 10m,
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, status: OfferStatus.Declined, offeredPrice: 20m,
            createdAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(20m, result.Data![0].OfferedPrice);
        Assert.Equal(10m, result.Data![1].OfferedPrice);
    }

    [Fact]
    public async Task GetReceivedAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetReceivedAsync(cts.Token));
    }

    #endregion

    #region GetSentAsync

    [Fact]
    public async Task GetSentAsync_WhenNoOffers_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetSentAsync_WhenHasOffers_ReturnsOnlyOffersWhereCurrentUserIsBuyer()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var seller = SeedUser(db, "Seller");
        var bottleSeller = SeedBottle(db, seller.Id, "Seller bottle");
        var bottleBuyer = SeedBottle(db, buyer.Id, "Buyer bottle");
        var sent = SeedOffer(db, bottleSeller.Id, buyer.Id, seller.Id);
        SeedOffer(db, bottleBuyer.Id, seller.Id, buyer.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(sent.Id, result.Data![0].Id);
        Assert.Equal("Seller bottle", result.Data![0].BottleName);
    }

    [Fact]
    public async Task GetSentAsync_WhenOfferSoftDeleted_ExcludesDeleted()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var seller = SeedUser(db, "Seller");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, isDeleted: true);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetSentAsync_WhenMultipleOffers_OrderedByCreatedAtDescending()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var seller = SeedUser(db, "Seller");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, offeredPrice: 10m,
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, status: OfferStatus.Declined, offeredPrice: 20m,
            createdAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(20m, result.Data![0].OfferedPrice);
        Assert.Equal(10m, result.Data![1].OfferedPrice);
    }

    [Fact]
    public async Task GetSentAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetSentAsync(cts.Token));
    }

    #endregion

    #region AcceptAsync

    [Fact]
    public async Task AcceptAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offer not found.", result.Error);
    }

    [Fact]
    public async Task AcceptAsync_WhenNotSeller_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the seller can accept this offer.", result.Error);
    }

    [Fact]
    public async Task AcceptAsync_WhenNotPending_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be accepted.", result.Error);
    }

    [Fact]
    public async Task AcceptAsync_WhenValid_SetsAcceptedAndNotifiesBuyer()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, name: "Yamazaki 12");
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Accepted, result.Data!.Status);
        Assert.NotNull(result.Data.RespondedAt);

        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.Accepted, stored!.Status);
        Assert.NotNull(stored.RespondedAt);

        notificationMock.Verify(n => n.CreateAsync(
            buyer.Id, NotificationType.OfferAccepted, offer.Id, "Yamazaki 12", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AcceptAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region DeclineAsync

    [Fact]
    public async Task DeclineAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var service = CreateOfferService(db, seller.Id);

        var result = await service.DeclineAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offer not found.", result.Error);
    }

    [Fact]
    public async Task DeclineAsync_WhenNotSeller_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the seller can decline this offer.", result.Error);
    }

    [Fact]
    public async Task DeclineAsync_WhenNotPending_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Withdrawn);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be declined.", result.Error);
    }

    [Fact]
    public async Task DeclineAsync_WhenValid_SetsDeclinedAndNotifiesBuyer()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, name: "Hibiki");
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Declined, result.Data!.Status);
        Assert.NotNull(result.Data.RespondedAt);

        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.Declined, stored!.Status);
        Assert.NotNull(stored.RespondedAt);

        notificationMock.Verify(n => n.CreateAsync(
            buyer.Id, NotificationType.OfferDeclined, offer.Id, "Hibiki", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeclineAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeclineAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region WithdrawAsync

    [Fact]
    public async Task WithdrawAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.WithdrawAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offer not found.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotBuyer_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the buyer can withdraw this offer.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotPending_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be withdrawn.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenValid_SetsWithdrawn()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Withdrawn, result.Data!.Status);

        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.Withdrawn, stored!.Status);
    }

    [Fact]
    public async Task WithdrawAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.WithdrawAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
