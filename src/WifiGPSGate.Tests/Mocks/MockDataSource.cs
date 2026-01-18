using WifiGPSGate.Core.Interfaces;

namespace WifiGPSGate.Tests.Mocks;

public class MockDataSource : IDataSource
{
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly List<ReadOnlyMemory<byte>> _dataToEmit = new();
    private CancellationTokenSource? _cts;

    public string Name => "MockSource";
    public ConnectionState State => _state;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public void QueueData(ReadOnlyMemory<byte> data)
    {
        _dataToEmit.Add(data);
    }

    public void QueueData(string data)
    {
        _dataToEmit.Add(System.Text.Encoding.ASCII.GetBytes(data));
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        SetState(ConnectionState.Connecting);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        SetState(ConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public void EmitQueuedData()
    {
        foreach (var data in _dataToEmit)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs
            {
                Data = data,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        _dataToEmit.Clear();
    }

    public void EmitData(ReadOnlyMemory<byte> data)
    {
        DataReceived?.Invoke(this, new DataReceivedEventArgs
        {
            Data = data,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    public void EmitData(string data)
    {
        EmitData(System.Text.Encoding.ASCII.GetBytes(data).AsMemory());
    }

    public void SimulateError(string message)
    {
        SetState(ConnectionState.Error, message);
    }

    private void SetState(ConnectionState newState, string? errorMessage = null)
    {
        var oldState = _state;
        if (oldState != newState)
        {
            _state = newState;
            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                ErrorMessage = errorMessage
            });
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
