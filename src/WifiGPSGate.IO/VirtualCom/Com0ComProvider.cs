using Microsoft.Win32;
using Serilog;

namespace WifiGPSGate.IO.VirtualCom;

/// <summary>
/// Provider for detecting and configuring com0com virtual serial port pairs.
/// com0com creates pairs of virtual COM ports where data written to one appears on the other.
/// </summary>
public static class Com0ComProvider
{
    private static readonly ILogger _logger = Log.Logger.ForContext(typeof(Com0ComProvider));

    private const string Com0ComRegistryPath = @"SYSTEM\CurrentControlSet\Enum\Root\PORTS";
    private const string Com0ComClassGuid = "{4D36E978-E325-11CE-BFC1-08002BE10318}";

    /// <summary>
    /// Checks if com0com is installed on the system.
    /// </summary>
    public static bool IsInstalled()
    {
        try
        {
            // Check for com0com in the registry
            using var portsKey = Registry.LocalMachine.OpenSubKey(Com0ComRegistryPath);
            if (portsKey == null)
            {
                return false;
            }

            foreach (var subKeyName in portsKey.GetSubKeyNames())
            {
                using var subKey = portsKey.OpenSubKey(subKeyName);
                var service = subKey?.GetValue("Service") as string;
                if (service?.Equals("com0com", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.Debug("com0com detected in registry");
                    return true;
                }
            }

            // Alternative check: Look for com0com port names
            var ports = System.IO.Ports.SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                if (IsCom0ComPort(port))
                {
                    _logger.Debug("com0com port detected: {Port}", port);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error checking for com0com installation");
            return false;
        }
    }

    /// <summary>
    /// Checks if a port is a com0com virtual port.
    /// com0com ports typically have names like CNCA0, CNCB0, etc.
    /// </summary>
    private static bool IsCom0ComPort(string portName)
    {
        if (string.IsNullOrEmpty(portName))
        {
            return false;
        }

        // com0com ports can have names like CNCA0, CNCB0, or standard COM names
        // Check registry for com0com ownership
        try
        {
            using var portsKey = Registry.LocalMachine.OpenSubKey(Com0ComRegistryPath);
            if (portsKey == null)
            {
                return false;
            }

            foreach (var subKeyName in portsKey.GetSubKeyNames())
            {
                using var subKey = portsKey.OpenSubKey(subKeyName);
                var portNameValue = subKey?.GetValue("PortName") as string;
                var service = subKey?.GetValue("Service") as string;

                if (portNameValue?.Equals(portName, StringComparison.OrdinalIgnoreCase) == true &&
                    service?.Equals("com0com", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore registry errors
        }

        return false;
    }

    /// <summary>
    /// Gets all available com0com port pairs.
    /// </summary>
    public static List<(string portA, string portB)> GetPortPairs()
    {
        var pairs = new List<(string, string)>();

        try
        {
            var ports = System.IO.Ports.SerialPort.GetPortNames()
                .Where(IsCom0ComPort)
                .OrderBy(p => p)
                .ToList();

            // com0com creates pairs, typically with sequential numbering
            // CNCA0/CNCB0, COM10/COM11, etc.
            for (int i = 0; i < ports.Count - 1; i += 2)
            {
                pairs.Add((ports[i], ports[i + 1]));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting com0com port pairs");
        }

        return pairs;
    }

    /// <summary>
    /// Finds an existing com0com port pair that includes the specified port name,
    /// or suggests a pair if one can be created.
    /// Returns (writePort, readPort) where writePort is the one WifiGPSGate writes to,
    /// and readPort is the one GPS applications should connect to.
    /// </summary>
    public static (string writePort, string readPort)? FindOrCreatePortPair(string preferredReadPort)
    {
        try
        {
            var existingPairs = GetPortPairs();

            // Look for a pair containing the preferred port
            foreach (var pair in existingPairs)
            {
                if (pair.portA.Equals(preferredReadPort, StringComparison.OrdinalIgnoreCase))
                {
                    return (pair.portB, pair.portA);
                }
                if (pair.portB.Equals(preferredReadPort, StringComparison.OrdinalIgnoreCase))
                {
                    return (pair.portA, pair.portB);
                }
            }

            // If we have any available pairs, use the first one
            if (existingPairs.Count > 0)
            {
                var firstPair = existingPairs[0];
                _logger.Information("Using existing com0com pair: {WritePort} -> {ReadPort}",
                    firstPair.portA, firstPair.portB);
                return (firstPair.portA, firstPair.portB);
            }

            _logger.Warning("No com0com port pairs found. Please configure com0com to create a port pair.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error finding com0com port pair");
            return null;
        }
    }

    /// <summary>
    /// Gets information about the com0com installation.
    /// </summary>
    public static string GetInstallationInfo()
    {
        if (!IsInstalled())
        {
            return "com0com is not installed";
        }

        var pairs = GetPortPairs();
        if (pairs.Count == 0)
        {
            return "com0com is installed but no port pairs are configured";
        }

        var pairStrings = pairs.Select(p => $"{p.portA} <-> {p.portB}");
        return $"com0com port pairs: {string.Join(", ", pairStrings)}";
    }
}
