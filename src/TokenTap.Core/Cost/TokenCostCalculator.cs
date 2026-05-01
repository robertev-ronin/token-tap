using TokenTap.Core.Models;

namespace TokenTap.Core.Cost;

public static class TokenCostCalculator
{
    public static decimal CalculateCostCents(
        string model,
        long inputTokens,
        long outputTokens,
        long cachedTokens,
        IReadOnlyDictionary<string, ModelPricing> pricingTable)
    {
        ModelPricing pricing = ResolvePricing(model, pricingTable);

        decimal dollars =
            (inputTokens / 1_000_000m * pricing.InputPerMillion) +
            (cachedTokens / 1_000_000m * pricing.CachedInputPerMillion) +
            (outputTokens / 1_000_000m * pricing.OutputPerMillion);

        return Math.Round(dollars * 100m, 6, MidpointRounding.AwayFromZero);
    }

    public static ModelPricing ResolvePricing(string model, IReadOnlyDictionary<string, ModelPricing> pricingTable)
    {
        if (pricingTable.TryGetValue(model, out ModelPricing? pricing))
        {
            return pricing;
        }

        if (pricingTable.TryGetValue("copilot-estimated", out ModelPricing? fallback) &&
            model.Contains("copilot", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        if (pricingTable.Count > 0)
        {
            return pricingTable.Values.First();
        }

        return new ModelPricing
        {
            Provider = "unknown",
            InputPerMillion = 0m,
            CachedInputPerMillion = 0m,
            OutputPerMillion = 0m
        };
    }
}
