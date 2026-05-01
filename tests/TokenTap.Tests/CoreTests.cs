using TokenTap.Core;
using TokenTap.Core.Config;
using TokenTap.Core.Cost;
using TokenTap.Core.Models;
using TokenTap.Core.Privacy;
using TokenTap.Core.Retention;

namespace TokenTap.Tests;

public sealed class CoreTests
{
    [Fact]
    public void CostCalculator_UsesConfiguredModelPricing()
    {
        TokenTapConfig config = TokenTapConfig.CreateDefault();

        decimal cents = TokenCostCalculator.CalculateCostCents(
            "gpt-5.4",
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedTokens: 100_000,
            config.Models);

        Assert.Equal(805m, cents);
    }

    [Fact]
    public void SecretRedactor_RemovesCommonSecretShapes()
    {
        string redacted = SecretRedactor.Redact("Authorization: Bearer sk-abcdefghijklmnopqrstuv token=ghp_abcdefghijklmnopqrstuvwxyz123456");

        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuv", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz123456", redacted, StringComparison.Ordinal);
        Assert.Contains("[redacted", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageEventFactory_FinalizesCostHashesExcerptAndFingerprint()
    {
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        UsageEvent usageEvent = new()
        {
            Model = "gpt-5.4",
            AgentName = "codex",
            Source = "test",
            InputTokens = 1_000_000,
            OutputTokens = 0
        };

        UsageEventFactory.FinalizeEvent(usageEvent, config, "prompt with password=secret");

        Assert.Equal(200m, usageEvent.EstimatedCostCents);
        Assert.NotNull(usageEvent.PromptHash);
        Assert.NotEmpty(usageEvent.EventFingerprint);
        Assert.DoesNotContain("secret", usageEvent.RawExcerptRedacted, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("14d", 14)]
    [InlineData("48h", 2)]
    public void DurationSpecParser_ParsesCommonUnits(string value, int expectedDays)
    {
        Assert.Equal(expectedDays, DurationSpecParser.Parse(value).TotalDays);
    }

    [Fact]
    public async Task ConfigManager_RoundTripsDefaultConfig()
    {
        string root = TestPaths.CreateDirectory();
        string configPath = Path.Combine(root, "token-tap.json");
        ConfigManager manager = new(configPath);
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        config.DefaultModel = "test-model";

        await manager.SaveAsync(config);
        TokenTapConfig loaded = await manager.LoadOrDefaultAsync();

        Assert.Equal("test-model", loaded.DefaultModel);
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public void ContentHasher_IsDeterministic()
    {
        Assert.Equal(ContentHasher.Sha256Hex("same"), ContentHasher.Sha256Hex("same"));
        Assert.NotEqual(ContentHasher.Sha256Hex("same"), ContentHasher.Sha256Hex("different"));
    }
}
