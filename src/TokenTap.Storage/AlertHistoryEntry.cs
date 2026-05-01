namespace TokenTap.Storage;

public sealed class AlertHistoryEntry
{
    public long? RuleId { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string Severity { get; set; } = "warning";

    public decimal TriggerValue { get; set; }

    public decimal Threshold { get; set; }

    public string Message { get; set; } = "";

    public bool WindowsSent { get; set; }

    public bool EmailSent { get; set; }

    public bool Suppressed { get; set; }

    public string? SuppressionReason { get; set; }
}
