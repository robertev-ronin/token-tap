namespace TokenTap.Alerts;

public sealed class ConsoleAlertNotifier : IAlertNotifier
{
    private readonly TextWriter _writer;

    public ConsoleAlertNotifier(TextWriter writer)
    {
        _writer = writer;
    }

    public Task NotifyAsync(AlertDecision decision, CancellationToken cancellationToken = default)
    {
        return _writer.WriteLineAsync($"[{decision.Rule.Severity}] {decision.Rule.Name}: {decision.Message}");
    }
}
