using TokenTap.Core.Models;

namespace TokenTap.Parsers;

public sealed class ParserContext
{
    public string SourcePath { get; init; } = "";

    public string DefaultAgent { get; init; } = "unknown";

    public string DefaultSource { get; init; } = "log";

    public TokenTapConfig Config { get; init; } = TokenTapConfig.CreateDefault();
}
