namespace TokenTap.Core.Models;

public sealed class UsageTotals
{
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

    public long TotalTokens => InputTokens + OutputTokens + CachedTokens;

    public decimal EstimatedCostDollars => EstimatedCostCents / 100m;
}
