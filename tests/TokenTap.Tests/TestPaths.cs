namespace TokenTap.Tests;

internal static class TestPaths
{
    public static string CreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "token-tap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
