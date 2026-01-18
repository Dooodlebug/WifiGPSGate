using System.Net;
using System.Net.Sockets;
using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.IO.Sinks;

public sealed class UdpDataSink : IDataSink
{
    private readonly UdpBroadcastOutputConfiguration _config;
    private readonly ILogger _logger;
    private UdpClient? _client;
    private IPEndPoint? _endpoint;
    private ConnectionState _state = ConnectionState.Disconnected;

    public string Name => $"UDP:{_config.DestinationAddress}:{_config.Port}";
    public ConnectionState State => _state;
    public bool IsReady => _state == ConnectionState.Connected && _client != null;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public UdpDataSink(UdpBroadcastOutputConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger?.ForContext<UdpDataSink>() ?? Log.Logger.ForContext<UdpDataSink>();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected)
        {
            return Task.CompletedTask;
        }

        SetState(ConnectionState.Connecting);

        try
        {
            _client = new UdpClient();

            if (_config.EnableBroadcast)
            {
                _client.EnableBroadcast = true;
            }

            // Parse the destination address
            if (!IPAddress.TryParse(_config.DestinationAddress, out var ipAddress))
            {
                // Try to resolve as hostname
                var addresses = Dns.GetHostAddresses(_config.DestinationAddress);
                if (addresses.Length == 0)
                {
                    throw new InvalidOperationException($"Could not resolve address: {_config.DestinationAddress}");
                }
                ipAddress = addresses[0];
            }

            _endpoint = new IPEndPoint(ipAddress, _config.Port);

            SetState(ConnectionState.Connected);
            _logger.Information("UDP broadcast sink started for {Address}:{Port}",
                _config.DestinationAddress, _config.Port);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start UDP broadcast sink for {Address}:{Port}",
                _config.DestinationAddress, _config.Port);
            SetState(ConnectionState.Error, ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Disconnected)
        {
            return Task.CompletedTask;
        }

        try
        {
            _client?.Close();
            _client?.Dispose();
            _client = null;
            _endpoint = null;

            SetState(ConnectionState.Disconnected);
            _logger.Information("UDP broadcast sink stopped for {Address}:{Port}",
                _config.DestinationAddress, _config.Port);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error closing UDP broadcast sink for {Address}:{Port}",
                _config.DestinationAddress, _config.Port);
        }

        return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!IsReady || _client == null || _endpoint == null)
        {
            throw new InvalidOperationException("UDP sink is not ready");
        }

        try
        {
            await _client.SendAsync(data, _endpoint, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending UDP data to {Address}:{Port}",
                _config.DestinationAddress, _config.Port);
            SetState(ConnectionState.Error, ex.Message);
            throw;
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
