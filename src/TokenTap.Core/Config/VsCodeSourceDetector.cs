namespace TokenTap.Core.Config;

public static class VsCodeSourceDetector
{
    public static IReadOnlyList<string> DetectExistingFolders(IEnumerable<string> configuredFolders)
    {
        List<string> folders = [];
        foreach (string folder in configuredFolders)
        {
            string expanded = EnvironmentPathExpander.Expand(folder);
            if (Directory.Exists(expanded))
            {
                folders.Add(Path.GetFullPath(expanded));
            }
        }

        return folders
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
