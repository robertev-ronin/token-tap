using System.Text.Json.Serialization;

namespace TokenTap.Core.Models;

public sealed class TokenTapConfig
{
    public string DatabasePath { get; set; } = "%USERPROFILE%\\.token-tap\\token-tap.db";

    public string DefaultCurrency { get; set; } = "USD";

    public string DefaultModel { get; set; } = "gpt-5.4";

    public Dictionary<string, ModelPricing> Models { get; set; } = CreateDefaultModels();

    public List<string> WatchFolders { get; set; } =
    [
        "%APPDATA%\\Code\\logs",
        "%APPDATA%\\Code - Insiders\\logs",
        "%APPDATA%\\Code\\User\\globalStorage",
        "%APPDATA%\\Code - Insiders\\User\\globalStorage"
    ];

    public PrivacyOptions Privacy { get; set; } = new();

    public EstimationOptions Estimation { get; set; } = new();

    public HistoryOptions History { get; set; } = new();

    public PerformanceCounterOptions PerformanceCounters { get; set; } = new();

    public AlertOptions Alerts { get; set; } = AlertOptions.CreateDefault();

    public static TokenTapConfig CreateDefault() => new();

    private static Dictionary<string, ModelPricing> CreateDefaultModels() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5.5"] = new()
            {
                Provider = "openai",
                InputPerMillion = 5.00m,
                CachedInputPerMillion = 0.50m,
                OutputPerMillion = 30.00m
            },
            ["gpt-5.4"] = new()
            {
                Provider = "openai",
                InputPerMillion = 2.00m,
                CachedInputPerMillion = 0.50m,
                OutputPerMillion = 12.00m
            },
            ["copilot-estimated"] = new()
            {
                Provider = "github",
                InputPerMillion = 2.00m,
                CachedInputPerMillion = 0.00m,
                OutputPerMillion = 10.00m
            }
        };
}

public sealed class PrivacyOptions
{
    public bool StoreFullPrompts { get; set; }

    public bool StoreFullResponses { get; set; }

    public bool StorePromptHashes { get; set; } = true;

    public bool StoreResponseHashes { get; set; } = true;

    public bool StoreShortExcerpts { get; set; } = true;

    public int MaxExcerptChars { get; set; } = 300;

    public bool RedactSecrets { get; set; } = true;

    public bool StoreRawLogLines { get; set; }
}

public sealed class EstimationOptions
{
    public int CharsPerToken { get; set; } = 4;

    public decimal DefaultOutputToInputRatio { get; set; } = 0.35m;

    public bool ConfidenceMode { get; set; } = true;
}

public sealed class HistoryOptions
{
    public int RetentionDays { get; set; } = 90;

    public int EventRetentionDays { get; set; } = 14;

    public int AnomalyRetentionDays { get; set; } = 90;

    public int AlertRetentionDays { get; set; } = 90;

    public int AggregateRetentionDays { get; set; } = 730;

    public int HourlyAggregateRetentionDays { get; set; } = 180;

    public bool AutoCleanup { get; set; } = true;

    public bool CleanupOnStartup { get; set; } = true;

    public int CleanupIntervalHours { get; set; } = 24;

    public int MaxDatabaseSizeMb { get; set; } = 250;

    public bool CompactAfterCleanup { get; set; } = true;
}

public sealed class PerformanceCounterOptions
{
    public bool Enabled { get; set; } = true;

    public int PublishIntervalSeconds { get; set; } = 10;

    public string CategoryName { get; set; } = "TokenTap";

    public string AgentCategoryName { get; set; } = "TokenTap Agent";
}

public sealed class AlertOptions
{
    public bool Enabled { get; set; } = true;

    public WindowsNotificationOptions WindowsNotifications { get; set; } = new();

    public EmailAlertOptions Email { get; set; } = new();

    public List<AlertRuleConfig> Rules { get; set; } = [];

    public static AlertOptions CreateDefault() =>
        new()
        {
            Rules =
            [
                new()
                {
                    Name = "Daily spend warning",
                    Enabled = true,
                    Type = "daily_cost",
                    Threshold = 25.00m,
                    Severity = "warning",
                    NotifyWindows = true,
                    NotifyEmail = false
                },
                new()
                {
                    Name = "Daily spend critical",
                    Enabled = true,
                    Type = "daily_cost",
                    Threshold = 50.00m,
                    Severity = "critical",
                    NotifyWindows = true,
                    NotifyEmail = true
                },
                new()
                {
                    Name = "Single session expensive",
                    Enabled = true,
                    Type = "session_cost",
                    Threshold = 10.00m,
                    Severity = "warning",
                    NotifyWindows = true,
                    NotifyEmail = false
                },
                new()
                {
                    Name = "Large prompt detected",
                    Enabled = true,
                    Type = "input_tokens",
                    Threshold = 100_000m,
                    Severity = "warning",
                    NotifyWindows = true,
                    NotifyEmail = false
                },
                new()
                {
                    Name = "Runaway agent suspected",
                    Enabled = true,
                    Type = "repeated_prompt_count",
                    Threshold = 8m,
                    WindowMinutes = 30,
                    Severity = "critical",
                    NotifyWindows = true,
                    NotifyEmail = true
                }
            ]
        };
}

public sealed class WindowsNotificationOptions
{
    public bool Enabled { get; set; } = true;

    public string AppName { get; set; } = "Token-Tap";

    public bool Sound { get; set; } = true;
}

public sealed class EmailAlertOptions
{
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = "smtp.example.com";

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = "alerts@example.com";

    public string PasswordSecretName { get; set; } = "TOKEN_TAP_SMTP_PASSWORD";

    public string From { get; set; } = "alerts@example.com";

    public List<string> To { get; set; } = [];

    public int MaxEmailsPer24Hours { get; set; } = 5;

    public int CooldownMinutes { get; set; } = 60;
}

public sealed class AlertRuleConfig
{
    [JsonIgnore]
    public long? Id { get; set; }

    public string Name { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public string Type { get; set; } = "";

    public decimal Threshold { get; set; }

    public int WindowMinutes { get; set; } = 60;

    public string Severity { get; set; } = "warning";

    public bool NotifyWindows { get; set; } = true;

    public bool NotifyEmail { get; set; }

    public int CooldownMinutes { get; set; } = 60;
}
