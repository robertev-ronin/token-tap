using System.Text.Json;

namespace TokenTap.Parsers;

internal static class JsonElementReader
{
    public static IEnumerable<JsonElement> ReadObjects(string content)
    {
        foreach (string line in SplitCandidates(content))
        {
            if (TryParseElement(line, out JsonElement element))
            {
                yield return element;
            }
        }
    }

    private static IEnumerable<string> SplitCandidates(string content)
    {
        string trimmed = content.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            yield break;
        }

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            yield return trimmed;
            yield break;
        }

        using StringReader reader = new(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.StartsWith('{') || line.StartsWith('['))
            {
                yield return line;
            }
        }
    }

    private static bool TryParseElement(string json, out JsonElement element)
    {
        element = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                JsonElement first = root.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    element = first.Clone();
                    return true;
                }
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                element = root.Clone();
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
