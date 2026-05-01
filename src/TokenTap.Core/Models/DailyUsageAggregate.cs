namespace TokenTap.Core.Models;

public sealed class DailyUsageAggregate
{
    public DateOnly Date { get; set; }

    public string AgentName { get; set; } = "unknown";

    public string Source { get; set; } = "unknown";

    public string RepoPathHash { get; set; } = "";

    public string Model { get; set; } = "unknown";

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedTokens { get; set; }

    public decimal EstimatedCostCents { get; set; }

    public long EventCount { get; set; }

    public long SessionCount { get; set; }

    public long LargePromptCount { get; set; }

    public long RepeatedPromptCount { get; set; }

    public long RunawayWarningCount { get; set; }

    public long UnknownModelCount { get; set; }

    public long ParserErrorCount { get; set; }

    public ConfidenceLevel ConfidenceMin { get; set; } = ConfidenceLevel.Estimated;

    public ConfidenceLevel ConfidenceMax { get; set; } = ConfidenceLevel.Estimated;
}
