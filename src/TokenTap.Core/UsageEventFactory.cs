using TokenTap.Core.Cost;
using TokenTap.Core.Models;
using TokenTap.Core.Privacy;

namespace TokenTap.Core;

public sealed class UsageEventFactory
{
    public static UsageEvent FinalizeEvent(UsageEvent usageEvent, TokenTapConfig config, string? rawLine = null)
    {
        usageEvent.Model = string.IsNullOrWhiteSpace(usageEvent.Model) || usageEvent.Model == "unknown"
            ? config.DefaultModel
            : usageEvent.Model;

        usageEvent.EstimatedCostCents = TokenCostCalculator.CalculateCostCents(
            usageEvent.Model,
            usageEvent.InputTokens,
            usageEvent.OutputTokens,
            usageEvent.CachedTokens,
            config.Models);

        if (!string.IsNullOrWhiteSpace(rawLine))
        {
            if (config.Privacy.StorePromptHashes)
            {
                usageEvent.PromptHash ??= ContentHasher.Sha256Hex(rawLine);
            }

            usageEvent.RawExcerptRedacted ??= SecretRedactor.BuildExcerpt(rawLine, config.Privacy);
        }

        usageEvent.EventFingerprint = string.IsNullOrWhiteSpace(usageEvent.EventFingerprint)
            ? BuildFingerprint(usageEvent)
            : usageEvent.EventFingerprint;

        return usageEvent;
    }

    public static string BuildFingerprint(UsageEvent usageEvent)
    {
        string material = string.Join(
            "|",
            usageEvent.Timestamp.ToUniversalTime().ToString("O"),
            usageEvent.Source,
            usageEvent.AgentName,
            usageEvent.Model,
            usageEvent.InputTokens,
            usageEvent.OutputTokens,
            usageEvent.CachedTokens,
            usageEvent.SourceFileHash,
            usageEvent.PromptHash,
            usageEvent.RawExcerptRedacted);

        return ContentHasher.Sha256Hex(material);
    }
}
