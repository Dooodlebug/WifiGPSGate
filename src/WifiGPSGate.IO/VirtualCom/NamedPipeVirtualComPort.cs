using System.IO.Pipes;
using Serilog;

namespace WifiGPSGate.IO.VirtualCom;

/// <summary>
/// Named pipe-based virtual COM port fallback implementation.
/// Creates a named pipe at \\.\pipe\WifiGPSGate_{PortName} that clients can connect to.
/// </summary>
public sealed class NamedPipeVirtualComPort : IVirtualComProvider
{
    private readonly string _portName;
    private readonly string _pipeName;
    private readonly ILogger _logger;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _connectionTask;
    private bool _clientConnected;

    public string PortName => $"\\\\.\\pipe\\{_pipeName}";
    public bool IsTrueComPort => false;
    public bool IsReady => _pipeServer != null && _clientConnected;

    public NamedPipeVirtualComPort(string portName, ILogger? logger = null)
    {
        _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        _pipeName = $"WifiGPSGate_{portName.Replace(":", "_")}";
        _logger = logger?.ForContext<NamedPipeVirtualComPort>() ?? Log.Logger.ForContext<NamedPipeVirtualComPort>();
    }

    public Task OpenAsync(CancellationToken ct = default)
    {
        if (_pipeServer != null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _pipeServer = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.Out,
            1, // Max one client
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _logger.Information("Named pipe virtual COM port created: {PipeName}", PortName);

        // Start waiting for connection in background
        _connectionTask = WaitForConnectionAsync(_cts.Token);

        return Task.CompletedTask;
    }

    private async Task WaitForConnectionAsync(CancellationToken ct)
    {
        try
        {
            _logger.Debug("Waiting for client connection on {PipeName}", PortName);
            await _pipeServer!.WaitForConnectionAsync(ct);
            _clientConnected = true;
            _logger.Information("Client connected to named pipe: {PipeName}", PortName);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Connection wait cancelled for {PipeName}", PortName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error waiting for connection on {PipeName}", PortName);
        }
    }

    public Task CloseAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();

        if (_pipeServer != null)
        {
            if (_pipeServer.IsConnected)
            {
                _pipeServer.Disconnect();
            }
            _pipeServer.Close();
            _pipeServer.Dispose();
            _pipeServer = null;
        }

        _clientConnected = false;
        _cts?.Dispose();
        _cts = null;

        _logger.Information("Named pipe virtual COM port closed: {PipeName}", PortName);
        return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_pipeServer == null)
        {
            throw new InvalidOperationException("Named pipe is not open");
        }

        if (!_clientConnected)
        {
            // No client connected, silently discard data
            return;
        }

        try
        {
            await _pipeServer.WriteAsync(data, ct);
            await _pipeServer.FlushAsync(ct);
        }
        catch (IOException ex) when (ex.Message.Contains("pipe is broken") || ex.Message.Contains("no process"))
        {
            // Client disconnected
            _logger.Debug("Client disconnected from {PipeName}", PortName);
            _clientConnected = false;

            // Restart waiting for a new connection
            _pipeServer.Disconnect();
            _connectionTask = WaitForConnectionAsync(_cts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to named pipe {PipeName}", PortName);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}
