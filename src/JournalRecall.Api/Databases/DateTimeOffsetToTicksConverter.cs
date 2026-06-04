using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JournalRecall.Api.Databases;

/// <summary>
/// Stores <see cref="DateTimeOffset"/> as UTC ticks (a long). SQLite can't ORDER BY or compare
/// DateTimeOffset, which the timeline (issue 0006) and later period roll-ups rely on; ticks are
/// chronologically sortable in SQL. We always store UTC, so the offset is dropped on the round trip.
/// </summary>
public sealed class DateTimeOffsetToTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
