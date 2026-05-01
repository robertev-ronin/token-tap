using TokenTap.Core.Models;

namespace TokenTap.Alerts;

public sealed class AlertDecision
{
    public AlertRuleConfig Rule { get; init; } = new();

    public decimal TriggerValue { get; init; }

    public string Message { get; init; } = "";

    public bool ShouldNotifyWindows => Rule.NotifyWindows;

    public bool ShouldNotifyEmail => Rule.NotifyEmail;
}
