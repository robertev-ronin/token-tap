using TokenTap.Core.Models;

namespace TokenTap.Counters;

public sealed class CounterSnapshot
{
    public UsageTotals Totals { get; init; } = new();

    public int ActiveSessions { get; init; }

    public long LastEventAgeSeconds { get; init; }

    public Dictionary<string, UsageTotals> AgentTotals { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
