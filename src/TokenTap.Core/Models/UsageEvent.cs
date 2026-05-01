namespace TokenTap.Core.Models;

public sealed class UsageEvent
{
    public long? Id { get; set; }

    public long? SessionId { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string EventType { get; set; } = "usage";

    public string Source { get; set; } = "unknown";

    public string AgentName { get; set; } = "unknown";

    public string Model { get; set; } = "unknown";

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedTokens { get; set; }

    public decimal EstimatedCostCents { get; set; }

    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Estimated;

    public string? PromptHash { get; set; }

    public string? ResponseHash { get; set; }

    public string? SourceFileHash { get; set; }

    public string? SourceFile { get; set; }

    public string? RawExcerptRedacted { get; set; }

    public string EventFingerprint { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public long TotalTokens => InputTokens + OutputTokens + CachedTokens;
}
