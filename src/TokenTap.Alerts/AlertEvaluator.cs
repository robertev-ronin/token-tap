using TokenTap.Core.Models;

namespace TokenTap.Alerts;

public sealed class AlertEvaluator
{
    public static IReadOnlyList<AlertDecision> Evaluate(TokenTapConfig config, UsageTotals todayTotals, IReadOnlyList<UsageEvent> recentEvents)
    {
        if (!config.Alerts.Enabled)
        {
            return [];
        }

        List<AlertDecision> decisions = [];
        foreach (AlertRuleConfig rule in config.Alerts.Rules.Where(r => r.Enabled))
        {
            AlertDecision? decision = EvaluateRule(rule, todayTotals, recentEvents);
            if (decision is not null)
            {
                decisions.Add(decision);
            }
        }

        return decisions;
    }

    private static AlertDecision? EvaluateRule(AlertRuleConfig rule, UsageTotals totals, IReadOnlyList<UsageEvent> recentEvents)
    {
        return rule.Type.Trim().ToLowerInvariant() switch
        {
            "daily_cost" => EvaluateThreshold(rule, totals.EstimatedCostDollars, $"AI token spend today is estimated at {totals.EstimatedCostDollars:C}."),
            "input_tokens" => EvaluateThreshold(rule, recentEvents.Count == 0 ? 0 : recentEvents.Max(e => e.InputTokens), "A large prompt was detected."),
            "session_cost" => EvaluateThreshold(rule, recentEvents.Count == 0 ? 0 : recentEvents.Sum(e => e.EstimatedCostCents) / 100m, "The current imported session exceeded its cost threshold."),
            "repeated_prompt_count" => EvaluateThreshold(rule, CountRepeatedPrompts(recentEvents), "Repeated prompt hashes suggest a possible loop."),
            _ => null
        };
    }

    private static AlertDecision? EvaluateThreshold(AlertRuleConfig rule, decimal value, string message) =>
        value >= rule.Threshold
            ? new AlertDecision
            {
                Rule = rule,
                TriggerValue = value,
                Message = $"{message} Threshold: {rule.Threshold}."
            }
            : null;

    private static decimal CountRepeatedPrompts(IReadOnlyList<UsageEvent> recentEvents)
    {
        return recentEvents
            .Where(e => !string.IsNullOrWhiteSpace(e.PromptHash))
            .GroupBy(e => e.PromptHash, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Sum(g => g.Count());
    }
}
