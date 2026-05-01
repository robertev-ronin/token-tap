namespace TokenTap.Core.Models;

public enum ConfidenceLevel
{
    Exact = 0,
    Estimated = 1,
    Inferred = 2,
    Low = 3
}

public static class ConfidenceLevelExtensions
{
    public static string ToStorageValue(this ConfidenceLevel confidence) =>
        confidence switch
        {
            ConfidenceLevel.Exact => "exact",
            ConfidenceLevel.Estimated => "estimated",
            ConfidenceLevel.Inferred => "inferred",
            ConfidenceLevel.Low => "low",
            _ => "estimated"
        };

    public static ConfidenceLevel Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "exact" => ConfidenceLevel.Exact,
            "estimated" => ConfidenceLevel.Estimated,
            "inferred" => ConfidenceLevel.Inferred,
            "low" => ConfidenceLevel.Low,
            _ => ConfidenceLevel.Estimated
        };
}
