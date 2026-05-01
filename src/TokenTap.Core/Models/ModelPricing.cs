namespace TokenTap.Core.Models;

public sealed class ModelPricing
{
    public string Provider { get; set; } = "openai";

    public decimal InputPerMillion { get; set; }

    public decimal CachedInputPerMillion { get; set; }

    public decimal OutputPerMillion { get; set; }

    public DateTimeOffset EffectiveDate { get; set; } = DateTimeOffset.UtcNow;
}
