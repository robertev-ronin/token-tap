namespace TokenTap.Storage;

public sealed class CleanupResult
{
    public bool DryRun { get; set; }

    public long EventsDeleted { get; set; }

    public long SessionsDeleted { get; set; }

    public long AnomaliesDeleted { get; set; }

    public long AlertsDeleted { get; set; }

    public decimal DatabaseSizeBeforeMb { get; set; }

    public decimal DatabaseSizeAfterMb { get; set; }

    public bool VacuumPerformed { get; set; }

    public bool Success { get; set; } = true;

    public string Message { get; set; } = "";
}
