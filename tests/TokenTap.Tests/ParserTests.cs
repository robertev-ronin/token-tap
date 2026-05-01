using TokenTap.Core.Models;
using TokenTap.Parsers;

namespace TokenTap.Tests;

public sealed class ParserTests
{
    [Fact]
    public void OpenAiJsonParser_ExtractsExactUsage()
    {
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        CompositeUsageParser parser = new([new OpenAiJsonParser()]);

        IReadOnlyList<UsageEvent> events = parser.Parse(
            """{"model":"gpt-5.4","created":1777671818,"usage":{"prompt_tokens":100,"completion_tokens":25,"prompt_tokens_details":{"cached_tokens":10}}}""",
            new ParserContext { SourcePath = "openai.jsonl", DefaultAgent = "codex", Config = config });

        UsageEvent usageEvent = Assert.Single(events);
        Assert.Equal(100, usageEvent.InputTokens);
        Assert.Equal(25, usageEvent.OutputTokens);
        Assert.Equal(10, usageEvent.CachedTokens);
        Assert.Equal(ConfidenceLevel.Exact, usageEvent.Confidence);
    }

    [Fact]
    public void AnthropicJsonParser_ExtractsCacheUsage()
    {
        CompositeUsageParser parser = new([new AnthropicJsonParser()]);

        IReadOnlyList<UsageEvent> events = parser.Parse(
            """{"model":"claude-test","usage":{"input_tokens":8,"output_tokens":4,"cache_read_input_tokens":2,"cache_creation_input_tokens":3}}""",
            new ParserContext { SourcePath = "anthropic.jsonl", Config = TokenTapConfig.CreateDefault() });

        UsageEvent usageEvent = Assert.Single(events);
        Assert.Equal(8, usageEvent.InputTokens);
        Assert.Equal(4, usageEvent.OutputTokens);
        Assert.Equal(5, usageEvent.CachedTokens);
    }

    [Fact]
    public void GenericTextParser_ExtractsTokenCountsAndModel()
    {
        CompositeUsageParser parser = new([new GenericTextParser()]);

        IReadOnlyList<UsageEvent> events = parser.Parse(
            "2026-05-01T12:00:00Z codex model=gpt-5.4 input tokens: 120 output tokens: 30 cached tokens: 5",
            new ParserContext { SourcePath = "codex.log", Config = TokenTapConfig.CreateDefault() });

        UsageEvent usageEvent = Assert.Single(events);
        Assert.Equal("codex", usageEvent.AgentName);
        Assert.Equal("gpt-5.4", usageEvent.Model);
        Assert.Equal(120, usageEvent.InputTokens);
        Assert.Equal(30, usageEvent.OutputTokens);
        Assert.Equal(5, usageEvent.CachedTokens);
    }

    [Fact]
    public async Task CsvUsageImporter_MapsCommonColumns()
    {
        string root = TestPaths.CreateDirectory();
        string csv = Path.Combine(root, "usage.csv");
        await File.WriteAllTextAsync(csv, "timestamp,agent,model,input_tokens,output_tokens,cached_tokens,confidence\n2026-05-01T12:00:00Z,codex,gpt-5.4,10,5,2,exact\n");

        IReadOnlyList<UsageEvent> events = await CsvUsageImporter.ImportAsync(csv, TokenTapConfig.CreateDefault());

        UsageEvent usageEvent = Assert.Single(events);
        Assert.Equal("codex", usageEvent.AgentName);
        Assert.Equal(10, usageEvent.InputTokens);
        Assert.Equal(ConfidenceLevel.Exact, usageEvent.Confidence);
    }
}
