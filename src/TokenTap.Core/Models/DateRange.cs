namespace TokenTap.Core.Models;

public sealed record DateRange(DateTimeOffset StartInclusive, DateTimeOffset EndExclusive, string Label)
{
    public static DateRange Today(TimeProvider? timeProvider = null)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetLocalNow();
        DateTimeOffset start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        return new DateRange(start, start.AddDays(1), "today");
    }

    public static DateRange ThisWeek(TimeProvider? timeProvider = null)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetLocalNow();
        int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        DateTimeOffset start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(-daysSinceMonday);
        return new DateRange(start, start.AddDays(7), "this-week");
    }

    public static DateRange ThisMonth(TimeProvider? timeProvider = null)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetLocalNow();
        DateTimeOffset start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        return new DateRange(start, start.AddMonths(1), "this-month");
    }

    public static DateRange LastDays(int days, TimeProvider? timeProvider = null)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Day count must be positive.");
        }

        DateTimeOffset end = (timeProvider ?? TimeProvider.System).GetLocalNow();
        return new DateRange(end.AddDays(-days), end, $"{days}-days");
    }
}
