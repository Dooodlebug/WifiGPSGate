namespace WifiGPSGate.Core.Interfaces;

public interface IDataSink : IAsyncDisposable
{
    string Name { get; }
    ConnectionState State { get; }
    bool IsReady { get; }
    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
}
