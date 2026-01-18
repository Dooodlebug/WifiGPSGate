using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Interfaces;

public interface ISessionManager : IAsyncDisposable
{
    SessionState State { get; }
    SessionStatistics Statistics { get; }
    event EventHandler<SessionStateChangedEventArgs>? StateChanged;
    event EventHandler<NmeaSentence>? SentenceReceived;
    Task StartAsync(SessionConfiguration config, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

public class SessionStateChangedEventArgs : EventArgs
{
    public required SessionState OldState { get; init; }
    public required SessionState NewState { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum SessionState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}
