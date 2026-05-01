using System.Globalization;
using System.Text.RegularExpressions;

namespace TokenTap.Core.Retention;

public static partial class DurationSpecParser
{
    public static TimeSpan Parse(string value)
    {
        Match match = DurationPattern().Match(value.Trim());
        if (!match.Success)
        {
            throw new FormatException($"Invalid duration '{value}'. Use forms like 14d, 24h, or 30m.");
        }

        int amount = int.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
        string unit = match.Groups["unit"].Value.ToLowerInvariant();

        return unit switch
        {
            "d" or "day" or "days" => TimeSpan.FromDays(amount),
            "h" or "hour" or "hours" => TimeSpan.FromHours(amount),
            "m" or "minute" or "minutes" => TimeSpan.FromMinutes(amount),
            _ => throw new FormatException($"Unsupported duration unit '{unit}'.")
        };
    }

    [GeneratedRegex(@"^(?<amount>\d+)\s*(?<unit>d|day|days|h|hour|hours|m|minute|minutes)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();
}
