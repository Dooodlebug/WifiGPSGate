using System.Net.Sockets;
using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.IO.Sources;

public sealed class TcpClientDataSource : IDataSource
{
    private readonly TcpClientInputConfiguration _config;
    private readonly ILogger _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private ConnectionState _state = ConnectionState.Disconnected;

    public string Name => $"TCP:{_config.Host}:{_config.Port}";
    public ConnectionState State => _state;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public TcpClientDataSource(TcpClientInputConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger?.ForContext<TcpClientDataSource>() ?? Log.Logger.ForContext<TcpClientDataSource>();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _receiveTask = ConnectAndReceiveLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Disconnected)
        {
            return;
        }

        _cts?.Cancel();

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            catch (TimeoutException)
            {
                _logger.Warning("Timeout waiting for receive task to complete");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        CloseConnection();
        _cts?.Dispose();
        _cts = null;

        SetState(ConnectionState.Disconnected);
        _logger.Information("TCP client stopped");
    }

    private async Task ConnectAndReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetState(ConnectionState.Connecting);
                _logger.Information("Connecting to {Host}:{Port}", _config.Host, _config.Port);

                _client = new TcpClient();
                await _client.ConnectAsync(_config.Host, _config.Port, ct);

                _stream = _client.GetStream();
                SetState(ConnectionState.Connected);
                _logger.Information("Connected to {Host}:{Port}", _config.Host, _config.Port);

                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Connection error to {Host}:{Port}", _config.Host, _config.Port);
                SetState(ConnectionState.Reconnecting, ex.Message);

                CloseConnection();

                if (!ct.IsCancellationRequested)
                {
                    _logger.Information("Reconnecting in {Delay}...", _config.ReconnectDelay);
                    try
                    {
                        await Task.Delay(_config.ReconnectDelay, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested && _stream != null)
        {
            int bytesRead = await _stream.ReadAsync(buffer, ct);

            if (bytesRead == 0)
            {
                _logger.Warning("Connection closed by remote host");
                throw new IOException("Connection closed by remote host");
            }

            var data = new byte[bytesRead];
            Array.Copy(buffer, data, bytesRead);

            DataReceived?.Invoke(this, new DataReceivedEventArgs
            {
                Data = data,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    private void CloseConnection()
    {
        _stream?.Close();
        _stream?.Dispose();
        _stream = null;

        _client?.Close();
        _client?.Dispose();
        _client = null;
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
