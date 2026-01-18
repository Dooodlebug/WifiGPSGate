using WifiGPSGate.Core.Interfaces;

namespace WifiGPSGate.Tests.Mocks;

public class MockDataSink : IDataSink
{
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly List<byte[]> _writtenData = new();
    private bool _simulateWriteError;
    private string? _writeErrorMessage;

    public string Name => "MockSink";
    public ConnectionState State => _state;
    public bool IsReady => _state == ConnectionState.Connected;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public IReadOnlyList<byte[]> WrittenData => _writtenData.AsReadOnly();
    public int WriteCount => _writtenData.Count;

    public Task StartAsync(CancellationToken ct = default)
    {
        SetState(ConnectionState.Connecting);
        SetState(ConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!IsReady)
        {
            throw new InvalidOperationException("Mock sink is not ready");
        }

        if (_simulateWriteError)
        {
            SetState(ConnectionState.Error, _writeErrorMessage);
            throw new InvalidOperationException(_writeErrorMessage ?? "Simulated write error");
        }

        _writtenData.Add(data.ToArray());
        return ValueTask.CompletedTask;
    }

    public void SimulateWriteError(string? message = null)
    {
        _simulateWriteError = true;
        _writeErrorMessage = message ?? "Simulated write error";
    }

    public void ClearWriteError()
    {
        _simulateWriteError = false;
        _writeErrorMessage = null;
    }

    public void Clear()
    {
        _writtenData.Clear();
    }

    public string GetWrittenDataAsString(int index)
    {
        return System.Text.Encoding.ASCII.GetString(_writtenData[index]);
    }

    public IEnumerable<string> GetAllWrittenDataAsStrings()
    {
        return _writtenData.Select(d => System.Text.Encoding.ASCII.GetString(d));
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
        return ValueTask.CompletedTask;
    }
}
