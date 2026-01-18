using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;
using WifiGPSGate.IO.VirtualCom;

namespace WifiGPSGate.IO.Sinks;

/// <summary>
/// Data sink that outputs to a virtual COM port.
/// Supports both com0com (true COM port) and named pipe (fallback) modes.
/// </summary>
public sealed class VirtualComDataSink : IDataSink
{
    private readonly VirtualComOutputConfiguration _config;
    private readonly ILogger _logger;
    private IVirtualComProvider? _provider;
    private ConnectionState _state = ConnectionState.Disconnected;

    public string Name { get; private set; }
    public ConnectionState State => _state;
    public bool IsReady => _state == ConnectionState.Connected && _provider?.IsReady == true;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public VirtualComDataSink(VirtualComOutputConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger?.ForContext<VirtualComDataSink>() ?? Log.Logger.ForContext<VirtualComDataSink>();
        Name = $"VirtualCOM:{config.PortName}";
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected)
        {
            return;
        }

        SetState(ConnectionState.Connecting);

        try
        {
            // Create the virtual COM port provider
            _provider = VirtualComProviderFactory.Create(_config.PortName, _config.AutoMode);

            await _provider.OpenAsync(ct);

            // Update name to reflect actual port
            Name = _provider.IsTrueComPort
                ? $"VirtualCOM:{_provider.PortName}"
                : $"VirtualCOM (Pipe):{_provider.PortName}";

            SetState(ConnectionState.Connected);

            if (_provider.IsTrueComPort)
            {
                _logger.Information("Virtual COM port opened using com0com. GPS apps should connect to: {PortName}",
                    _provider.PortName);
            }
            else
            {
                _logger.Information("Virtual COM port opened using named pipe. Connect to: {PortName}",
                    _provider.PortName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start virtual COM port {Port}", _config.PortName);
            SetState(ConnectionState.Error, ex.Message);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Disconnected)
        {
            return;
        }

        try
        {
            if (_provider != null)
            {
                await _provider.CloseAsync(ct);
                await _provider.DisposeAsync();
                _provider = null;
            }

            SetState(ConnectionState.Disconnected);
            _logger.Information("Virtual COM port closed: {Port}", _config.PortName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error closing virtual COM port {Port}", _config.PortName);
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_provider == null)
        {
            throw new InvalidOperationException("Virtual COM port is not open");
        }

        try
        {
            await _provider.WriteAsync(data, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to virtual COM port {Port}", _config.PortName);
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

    /// <summary>
    /// Gets information about the current virtual COM port mode.
    /// </summary>
    public string GetModeInfo()
    {
        if (_provider == null)
        {
            return "Not connected";
        }

        return _provider.IsTrueComPort
            ? $"Using com0com. GPS apps connect to: {_provider.PortName}"
            : $"Using named pipe mode. Connect to: {_provider.PortName}";
    }
}
