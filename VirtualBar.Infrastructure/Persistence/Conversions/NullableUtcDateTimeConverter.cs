using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VirtualBar.Infrastructure.Persistence.Conversions;

/// <summary>
/// The nullable counterpart of <see cref="UtcDateTimeConverter"/> — EF Core matches converters by the
/// exact CLR type, so <c>DateTime?</c> properties (<c>DeletedAt</c>, <c>RespondedAt</c>,
/// <c>ForSaleAt</c>, …) need their own registration or they keep round-tripping as Unspecified.
/// </summary>
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            value => value.HasValue && value.Value.Kind == DateTimeKind.Local
                ? value.Value.ToUniversalTime()
                : value,
            value => value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value)
    {
    }
}
