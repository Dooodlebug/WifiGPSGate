namespace WifiGPSGate.Core.Interfaces;

public interface IDataSource : IAsyncDisposable
{
    string Name { get; }
    ConnectionState State { get; }
    event EventHandler<DataReceivedEventArgs>? DataReceived;
    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

public class DataReceivedEventArgs : EventArgs
{
    public required ReadOnlyMemory<byte> Data { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public class ConnectionStateChangedEventArgs : EventArgs
{
    public required ConnectionState OldState { get; init; }
    public required ConnectionState NewState { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
