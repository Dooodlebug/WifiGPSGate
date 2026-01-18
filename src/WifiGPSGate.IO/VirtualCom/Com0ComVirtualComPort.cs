using RJCP.IO.Ports;
using Serilog;

namespace WifiGPSGate.IO.VirtualCom;

/// <summary>
/// com0com-based virtual COM port implementation.
/// Uses a com0com port pair where WifiGPSGate writes to one port (writePort)
/// and GPS applications read from the other port (readPort).
/// </summary>
public sealed class Com0ComVirtualComPort : IVirtualComProvider
{
    private readonly string _writePort;
    private readonly string _readPort;
    private readonly ILogger _logger;
    private SerialPortStream? _serialPort;

    /// <summary>
    /// The port name that GPS applications should connect to.
    /// </summary>
    public string PortName => _readPort;

    public bool IsTrueComPort => true;
    public bool IsReady => _serialPort?.IsOpen == true;

    /// <summary>
    /// Creates a new com0com virtual COM port.
    /// </summary>
    /// <param name="writePort">The port WifiGPSGate writes to.</param>
    /// <param name="readPort">The port GPS applications connect to.</param>
    /// <param name="logger">Optional logger.</param>
    public Com0ComVirtualComPort(string writePort, string readPort, ILogger? logger = null)
    {
        _writePort = writePort ?? throw new ArgumentNullException(nameof(writePort));
        _readPort = readPort ?? throw new ArgumentNullException(nameof(readPort));
        _logger = logger?.ForContext<Com0ComVirtualComPort>() ?? Log.Logger.ForContext<Com0ComVirtualComPort>();
    }

    public Task OpenAsync(CancellationToken ct = default)
    {
        if (_serialPort?.IsOpen == true)
        {
            return Task.CompletedTask;
        }

        try
        {
            // Standard GPS settings: 4800 or 9600 baud, 8N1
            // Using 115200 for better throughput with modern GNSS receivers
            _serialPort = new SerialPortStream(_writePort, 115200, 8,
                RJCP.IO.Ports.Parity.None, RJCP.IO.Ports.StopBits.One);

            _serialPort.Open();

            _logger.Information("com0com virtual COM port opened: writing to {WritePort}, applications read from {ReadPort}",
                _writePort, _readPort);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open com0com port {WritePort}", _writePort);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken ct = default)
    {
        if (_serialPort != null)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error closing com0com port {WritePort}", _writePort);
            }
            finally
            {
                _serialPort = null;
            }

            _logger.Information("com0com virtual COM port closed: {WritePort}", _writePort);
        }

        return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("com0com port is not open");
        }

        try
        {
            await _serialPort.WriteAsync(data, ct);
            await _serialPort.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to com0com port {WritePort}", _writePort);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}
