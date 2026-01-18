using System.Net;
using System.Net.Sockets;
using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.IO.Sources;

public sealed class UdpDataSource : IDataSource
{
    private readonly UdpInputConfiguration _config;
    private readonly ILogger _logger;
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private ConnectionState _state = ConnectionState.Disconnected;

    public string Name => $"UDP:{_config.Port}";
    public ConnectionState State => _state;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public UdpDataSource(UdpInputConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger?.ForContext<UdpDataSource>() ?? Log.Logger.ForContext<UdpDataSource>();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
        {
            return Task.CompletedTask;
        }

        SetState(ConnectionState.Connecting);

        try
        {
            var endpoint = string.IsNullOrEmpty(_config.BindAddress)
                ? new IPEndPoint(IPAddress.Any, _config.Port)
                : new IPEndPoint(IPAddress.Parse(_config.BindAddress), _config.Port);

            _client = new UdpClient(endpoint);
            _cts = new CancellationTokenSource();

            _receiveTask = ReceiveLoopAsync(_cts.Token);

            SetState(ConnectionState.Connected);
            _logger.Information("UDP listener started on port {Port}", _config.Port);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start UDP listener on port {Port}", _config.Port);
            SetState(ConnectionState.Error, ex.Message);
            throw;
        }

        return Task.CompletedTask;
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

        _client?.Close();
        _client?.Dispose();
        _client = null;

        _cts?.Dispose();
        _cts = null;

        SetState(ConnectionState.Disconnected);
        _logger.Information("UDP listener stopped");
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _client != null)
        {
            try
            {
                var result = await _client.ReceiveAsync(ct);

                if (result.Buffer.Length > 0)
                {
                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        Data = result.Buffer,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error receiving UDP data");
                SetState(ConnectionState.Error, ex.Message);
            }
        }
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
