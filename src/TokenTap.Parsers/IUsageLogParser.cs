using TokenTap.Core.Models;

namespace TokenTap.Parsers;

public interface IUsageLogParser
{
    string Name { get; }

    IReadOnlyList<UsageEvent> Parse(string content, ParserContext context);
}
