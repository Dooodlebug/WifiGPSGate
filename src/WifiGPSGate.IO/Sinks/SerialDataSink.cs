using RJCP.IO.Ports;
using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.IO.Sinks;

public sealed class SerialDataSink : IDataSink
{
    private readonly SerialOutputConfiguration _config;
    private readonly ILogger _logger;
    private SerialPortStream? _port;
    private ConnectionState _state = ConnectionState.Disconnected;

    public string Name => $"Serial:{_config.PortName}";
    public ConnectionState State => _state;
    public bool IsReady => _state == ConnectionState.Connected && _port?.IsOpen == true;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public SerialDataSink(SerialOutputConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger?.ForContext<SerialDataSink>() ?? Log.Logger.ForContext<SerialDataSink>();
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
            _port = new SerialPortStream(_config.PortName, _config.BaudRate, _config.DataBits,
                MapParity(_config.Parity), MapStopBits(_config.StopBits));

            _port.Open();

            SetState(ConnectionState.Connected);
            _logger.Information("Serial port {Port} opened at {BaudRate} baud", _config.PortName, _config.BaudRate);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open serial port {Port}", _config.PortName);
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
            _port?.Close();
            _port?.Dispose();
            _port = null;

            SetState(ConnectionState.Disconnected);
            _logger.Information("Serial port {Port} closed", _config.PortName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error closing serial port {Port}", _config.PortName);
        }

        return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!IsReady || _port == null)
        {
            throw new InvalidOperationException("Serial port is not ready");
        }

        try
        {
            await _port.WriteAsync(data, ct);
            await _port.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to serial port {Port}", _config.PortName);
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

    private static RJCP.IO.Ports.Parity MapParity(Core.Models.Parity parity) => parity switch
    {
        Core.Models.Parity.None => RJCP.IO.Ports.Parity.None,
        Core.Models.Parity.Odd => RJCP.IO.Ports.Parity.Odd,
        Core.Models.Parity.Even => RJCP.IO.Ports.Parity.Even,
        Core.Models.Parity.Mark => RJCP.IO.Ports.Parity.Mark,
        Core.Models.Parity.Space => RJCP.IO.Ports.Parity.Space,
        _ => RJCP.IO.Ports.Parity.None
    };

    private static RJCP.IO.Ports.StopBits MapStopBits(Core.Models.StopBits stopBits) => stopBits switch
    {
        Core.Models.StopBits.One => RJCP.IO.Ports.StopBits.One,
        Core.Models.StopBits.OnePointFive => RJCP.IO.Ports.StopBits.One5,
        Core.Models.StopBits.Two => RJCP.IO.Ports.StopBits.Two,
        _ => RJCP.IO.Ports.StopBits.One
    };

    public static string[] GetAvailablePorts()
    {
        return System.IO.Ports.SerialPort.GetPortNames();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
