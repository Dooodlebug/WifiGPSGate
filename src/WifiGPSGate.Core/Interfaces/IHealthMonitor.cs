using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Interfaces;

public interface IHealthMonitor
{
    HealthStatus CurrentStatus { get; }
    DateTimeOffset LastDataReceived { get; }
    double DataRateHz { get; }
    event EventHandler<HealthChangedEventArgs>? HealthChanged;
    void RecordSentence(NmeaSentence sentence);
    void Reset();
}

public class HealthChangedEventArgs : EventArgs
{
    public required HealthStatus OldStatus { get; init; }
    public required HealthStatus NewStatus { get; init; }
}

public enum HealthStatus
{
    Unknown,
    Healthy,
    Stale,
    Error
}
