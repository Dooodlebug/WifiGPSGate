using System.Text.Json.Serialization;

namespace WifiGPSGate.Core.Models;

/// <summary>
/// Serializable user settings for persistence across app sessions.
/// </summary>
public sealed class UserSettings
{
    // Input settings
    public bool UseUdpInput { get; set; } = true;
    public bool UseTcpInput { get; set; }
    public int UdpPort { get; set; } = 9001;
    public string TcpHost { get; set; } = "192.168.1.1";
    public int TcpPort { get; set; } = 9001;

    // Serial output settings
    public bool EnableSerialOutput { get; set; } = true;
    public string SelectedSerialPort { get; set; } = "";
    public int SelectedBaudRate { get; set; } = 115200;

    // File output settings
    public bool EnableFileOutput { get; set; }
    public string LogFilePath { get; set; } = "";

    // UDP broadcast output settings
    public bool EnableUdpBroadcastOutput { get; set; }
    public string UdpBroadcastAddress { get; set; } = "255.255.255.255";
    public int UdpBroadcastPort { get; set; } = 9002;

    // Virtual COM port settings
    public bool EnableVirtualComOutput { get; set; }
    public string VirtualComPortName { get; set; } = "COM10";
    public bool VirtualComAutoMode { get; set; } = true;

    // Settings version for future migrations
    [JsonPropertyName("$version")]
    public int Version { get; set; } = 1;
}
