using System.Text.Json;
using TokenTap.Core.Models;

namespace TokenTap.Core.Config;

public sealed class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigManager(string? configPath = null)
    {
        ConfigPath = configPath is null
            ? GetDefaultConfigPath()
            : EnvironmentPathExpander.ExpandToFullPath(configPath);
    }

    public static string GetDefaultConfigPath() =>
        EnvironmentPathExpander.ExpandToFullPath("%USERPROFILE%\\.token-tap\\token-tap.json");

    public async Task<TokenTapConfig> LoadOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return TokenTapConfig.CreateDefault();
        }

        await using FileStream stream = File.OpenRead(ConfigPath);
        TokenTapConfig? config = await JsonSerializer.DeserializeAsync<TokenTapConfig>(stream, JsonOptions, cancellationToken);
        return Normalize(config ?? TokenTapConfig.CreateDefault());
    }

    public async Task SaveAsync(TokenTapConfig config, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async Task<TokenTapConfig> EnsureExistsAsync(CancellationToken cancellationToken = default)
    {
        TokenTapConfig config = await LoadOrDefaultAsync(cancellationToken);
        await SaveAsync(config, cancellationToken);
        return config;
    }

    private static TokenTapConfig Normalize(TokenTapConfig config)
    {
        config.Models = new Dictionary<string, ModelPricing>(config.Models, StringComparer.OrdinalIgnoreCase);
        return config;
    }
}
