using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Persistence.Conversions;

namespace VirtualBar.Tests.Persistence;

/// <summary>
/// Pins the UTC round-trip that <see cref="AppDbContext.ConfigureConventions"/> installs. A provider
/// column carries no offset, so without the converters EF materialises every timestamp as
/// <see cref="DateTimeKind.Unspecified"/>, System.Text.Json drops the "Z", and clients read UTC as
/// local time.
/// </summary>
public sealed class UtcDateTimeConverterTests
{
    private static readonly DateTime Utc = new(2026, 8, 1, 6, 41, 46, DateTimeKind.Utc);

    // A relational store is mandatory here: EF InMemory hands back the very CLR instance it was given,
    // so it preserves Kind on its own and would make every assertion below pass vacuously.
    private static AppDbContext CreateSqliteDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    #region UtcDateTimeConverter

    [Fact]
    public void ToProvider_WhenKindIsUtc_StoresValueUnchanged()
    {
        var convert = new UtcDateTimeConverter().ConvertToProviderExpression.Compile();

        var stored = convert(Utc);

        Assert.Equal(Utc, stored);
        Assert.Equal(6, stored.Hour);
    }

    [Fact]
    public void ToProvider_WhenKindIsLocal_ConvertsToUniversalTime()
    {
        var convert = new UtcDateTimeConverter().ConvertToProviderExpression.Compile();
        var local = Utc.ToLocalTime();

        var stored = convert(local);

        Assert.Equal(Utc, stored);
    }

    [Fact]
    public void ToProvider_WhenKindIsUnspecified_StoresValueUnchangedWithoutShifting()
    {
        // The safety branch: an Unspecified value is assumed to be UTC already (everything here comes
        // from DateTime.UtcNow). Calling ToUniversalTime() on it would silently shift the value by the
        // server's own offset and corrupt data.
        var convert = new UtcDateTimeConverter().ConvertToProviderExpression.Compile();
        var unspecified = DateTime.SpecifyKind(Utc, DateTimeKind.Unspecified);

        var stored = convert(unspecified);

        Assert.Equal(unspecified, stored);
        Assert.Equal(6, stored.Hour);
    }

    [Fact]
    public void FromProvider_StampsTheValueAsUtc()
    {
        var convert = new UtcDateTimeConverter().ConvertFromProviderExpression.Compile();
        var fromDatabase = DateTime.SpecifyKind(Utc, DateTimeKind.Unspecified);

        var read = convert(fromDatabase);

        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(Utc, read);
    }

    #endregion

    #region NullableUtcDateTimeConverter

    [Fact]
    public void NullableToProvider_WhenNull_StaysNull()
    {
        var convert = new NullableUtcDateTimeConverter().ConvertToProviderExpression.Compile();

        Assert.Null(convert(null));
    }

    [Fact]
    public void NullableToProvider_WhenKindIsLocal_ConvertsToUniversalTime()
    {
        var convert = new NullableUtcDateTimeConverter().ConvertToProviderExpression.Compile();

        Assert.Equal(Utc, convert(Utc.ToLocalTime()));
    }

    [Fact]
    public void NullableToProvider_WhenKindIsUtcOrUnspecified_StoresValueUnchanged()
    {
        var convert = new NullableUtcDateTimeConverter().ConvertToProviderExpression.Compile();
        var unspecified = DateTime.SpecifyKind(Utc, DateTimeKind.Unspecified);

        Assert.Equal(Utc, convert(Utc));
        Assert.Equal(unspecified, convert(unspecified));
    }

    [Fact]
    public void NullableFromProvider_WhenNull_StaysNull()
    {
        var convert = new NullableUtcDateTimeConverter().ConvertFromProviderExpression.Compile();

        Assert.Null(convert(null));
    }

    [Fact]
    public void NullableFromProvider_WhenValuePresent_StampsItAsUtc()
    {
        var convert = new NullableUtcDateTimeConverter().ConvertFromProviderExpression.Compile();
        var fromDatabase = DateTime.SpecifyKind(Utc, DateTimeKind.Unspecified);

        var read = convert(fromDatabase);

        Assert.Equal(DateTimeKind.Utc, read!.Value.Kind);
        Assert.Equal(Utc, read.Value);
    }

    #endregion

    #region Round-trip through the real model

    [Fact]
    public async Task DatabaseRoundTrip_ReturnsEveryDateTimeAsUtc()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await using (var write = CreateSqliteDbContext(connection))
        {
            write.Distilleries.Add(new Distillery
            {
                Name = "Ardbeg",
                CreatedAt = Utc,
                UpdatedAt = Utc,
                DeletedAt = Utc,
            });
            await write.SaveChangesAsync();
        }

        await using var read = CreateSqliteDbContext(connection);
        var stored = await read.Distilleries.AsNoTracking().SingleAsync();

        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.DeletedAt!.Value.Kind);
        Assert.Equal(Utc, stored.CreatedAt);
        Assert.Equal(Utc, stored.DeletedAt.Value);
    }

    [Fact]
    public async Task DatabaseRoundTrip_SerialisesWithTheUtcMarker()
    {
        // The actual user-visible symptom: an offset-less string makes `new Date(iso)` in the browser
        // read UTC as local time, so a notification seconds old rendered as "3h ago" in UTC+3.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await using (var write = CreateSqliteDbContext(connection))
        {
            write.Distilleries.Add(new Distillery { Name = "Ardbeg", CreatedAt = Utc, UpdatedAt = Utc });
            await write.SaveChangesAsync();
        }

        await using var read = CreateSqliteDbContext(connection);
        var stored = await read.Distilleries.AsNoTracking().SingleAsync();

        var json = JsonSerializer.Serialize(stored.CreatedAt);

        Assert.Equal("\"2026-08-01T06:41:46Z\"", json);
    }

    #endregion
}
