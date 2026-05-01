namespace TokenTap.Alerts;

public interface IAlertNotifier
{
    Task NotifyAsync(AlertDecision decision, CancellationToken cancellationToken = default);
}
