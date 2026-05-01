using System.Text.RegularExpressions;

namespace TokenTap.Core.Config;

public static partial class EnvironmentPathExpander
{
    public static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        string expanded = EnvironmentVariablePattern().Replace(path, match =>
        {
            string? value = Environment.GetEnvironmentVariable(match.Groups["name"].Value);
            return value ?? match.Value;
        });

        if (expanded == "~" || expanded.StartsWith("~\\", StringComparison.Ordinal) || expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = Path.Combine(home, expanded[1..].TrimStart('\\', '/'));
        }

        return Environment.ExpandEnvironmentVariables(expanded);
    }

    public static string ExpandToFullPath(string path)
    {
        string expanded = Expand(path);
        return Path.GetFullPath(expanded);
    }

    [GeneratedRegex("%(?<name>[A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariablePattern();
}
