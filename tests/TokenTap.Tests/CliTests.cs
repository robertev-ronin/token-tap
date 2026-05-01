using TokenTap.Cli;

namespace TokenTap.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task Cli_Init_CreatesConfigAndDatabase()
    {
        string root = TestPaths.CreateDirectory();
        string configPath = Path.Combine(root, "token-tap.json");
        using TestConsole console = new();

        int exitCode = await new TokenTapCli(console).RunAsync(["init", "--config", configPath, "--database", Path.Combine(root, "token-tap.db")]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(configPath));
        Assert.Contains("Token-Tap initialized", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_Help_PrintsCommandList()
    {
        using TestConsole console = new();

        int exitCode = await new TokenTapCli(console).RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("token-tap import log", console.Output, StringComparison.Ordinal);
    }

    private sealed class TestConsole : IConsole, IDisposable
    {
        private readonly StringWriter _out = new();
        private readonly StringWriter _error = new();

        public TextWriter Out => _out;

        public TextWriter ErrorOut => _error;

        public string Output => _out.ToString();

        public string Error => _error.ToString();

        public void Dispose()
        {
            _out.Dispose();
            _error.Dispose();
        }
    }
}
