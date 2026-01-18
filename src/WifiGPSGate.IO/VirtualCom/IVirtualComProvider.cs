namespace WifiGPSGate.IO.VirtualCom;

/// <summary>
/// Interface for virtual COM port providers.
/// </summary>
public interface IVirtualComProvider : IAsyncDisposable
{
    /// <summary>
    /// The name of the virtual COM port (e.g., "COM10" or "\\.\pipe\WifiGPSGate_COM10").
    /// </summary>
    string PortName { get; }

    /// <summary>
    /// Indicates whether this provider uses a true COM port (com0com) or a named pipe fallback.
    /// </summary>
    bool IsTrueComPort { get; }

    /// <summary>
    /// Indicates whether the virtual port is ready for writing.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Opens the virtual COM port.
    /// </summary>
    Task OpenAsync(CancellationToken ct = default);

    /// <summary>
    /// Closes the virtual COM port.
    /// </summary>
    Task CloseAsync(CancellationToken ct = default);

    /// <summary>
    /// Writes data to the virtual COM port.
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
}

/// <summary>
/// Factory for creating virtual COM port providers.
/// </summary>
public static class VirtualComProviderFactory
{
    /// <summary>
    /// Creates a virtual COM port provider.
    /// If com0com is available and autoMode is true, uses com0com.
    /// Otherwise falls back to named pipes.
    /// </summary>
    public static IVirtualComProvider Create(string portName, bool autoMode = true)
    {
        if (autoMode && Com0ComProvider.IsInstalled())
        {
            var portPair = Com0ComProvider.FindOrCreatePortPair(portName);
            if (portPair != null)
            {
                return new Com0ComVirtualComPort(portPair.Value.writePort, portPair.Value.readPort);
            }
        }

        return new NamedPipeVirtualComPort(portName);
    }

    /// <summary>
    /// Creates a named pipe virtual COM port (fallback mode).
    /// </summary>
    public static IVirtualComProvider CreateNamedPipe(string portName)
    {
        return new NamedPipeVirtualComPort(portName);
    }

    /// <summary>
    /// Creates a com0com virtual COM port if available.
    /// </summary>
    public static IVirtualComProvider? CreateCom0Com(string portName)
    {
        if (!Com0ComProvider.IsInstalled())
        {
            return null;
        }

        var portPair = Com0ComProvider.FindOrCreatePortPair(portName);
        if (portPair == null)
        {
            return null;
        }

        return new Com0ComVirtualComPort(portPair.Value.writePort, portPair.Value.readPort);
    }
}
