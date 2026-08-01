using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VirtualBar.Infrastructure.Persistence.Conversions;

/// <summary>
/// Keeps <see cref="DateTime"/> values UTC across a database round-trip.
/// <para>
/// SQL Server's <c>datetime2</c> carries no offset, so EF Core materialises every value with
/// <see cref="DateTimeKind.Unspecified"/>. System.Text.Json then writes it without the trailing "Z",
/// and a browser parsing that string treats UTC as local time — every timestamp in the API was
/// shifted by the client's offset (a notification seconds old rendered as "3h ago" in UTC+3).
/// </para>
/// <para>
/// Reading always re-stamps the value as UTC. Writing only converts a value that is explicitly
/// <see cref="DateTimeKind.Local"/>; an <see cref="DateTimeKind.Unspecified"/> value is stored as-is
/// on the assumption that it already is UTC (everything in this codebase originates from
/// <c>DateTime.UtcNow</c>). Never silently shift an Unspecified value — that would corrupt data
/// whenever the server's own offset is non-zero.
/// </para>
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
