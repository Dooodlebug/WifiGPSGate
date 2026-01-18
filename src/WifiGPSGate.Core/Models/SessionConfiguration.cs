namespace WifiGPSGate.Core.Models;

public sealed class SessionConfiguration
{
    public required InputConfiguration Input { get; init; }
    public required List<OutputConfiguration> Outputs { get; init; }
    public SentenceFilterConfiguration? Filter { get; init; }
    public RateLimiterConfiguration? RateLimiter { get; init; }
    public LoggingConfiguration? Logging { get; init; }
}

public abstract class InputConfiguration
{
    public abstract InputType Type { get; }
}

public sealed class UdpInputConfiguration : InputConfiguration
{
    public override InputType Type => InputType.Udp;
    public required int Port { get; init; }
    public string? BindAddress { get; init; }
}

public sealed class TcpClientInputConfiguration : InputConfiguration
{
    public override InputType Type => InputType.TcpClient;
    public required string Host { get; init; }
    public required int Port { get; init; }
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
}

public enum InputType
{
    Udp,
    TcpClient
}

public abstract class OutputConfiguration
{
    public abstract OutputType Type { get; }
    public bool Enabled { get; init; } = true;
}

public sealed class SerialOutputConfiguration : OutputConfiguration
{
    public override OutputType Type => OutputType.Serial;
    public required string PortName { get; init; }
    public int BaudRate { get; init; } = 115200;
    public int DataBits { get; init; } = 8;
    public Parity Parity { get; init; } = Parity.None;
    public StopBits StopBits { get; init; } = StopBits.One;
}

public sealed class FileOutputConfiguration : OutputConfiguration
{
    public override OutputType Type => OutputType.File;
    public required string FilePath { get; init; }
    public bool AppendTimestamp { get; init; } = true;
}

public sealed class UdpBroadcastOutputConfiguration : OutputConfiguration
{
    public override OutputType Type => OutputType.UdpBroadcast;
    public required string DestinationAddress { get; init; }
    public required int Port { get; init; }
    public bool EnableBroadcast { get; init; } = true;
}

public sealed class VirtualComOutputConfiguration : OutputConfiguration
{
    public override OutputType Type => OutputType.VirtualCom;
    public required string PortName { get; init; }
    public bool AutoMode { get; init; } = true;
}

public enum OutputType
{
    Serial,
    VirtualCom,
    File,
    UdpBroadcast
}

public enum Parity
{
    None,
    Odd,
    Even,
    Mark,
    Space
}

public enum StopBits
{
    One,
    OnePointFive,
    Two
}

public sealed class SentenceFilterConfiguration
{
    public HashSet<string> AllowedTypes { get; init; } = new();
    public HashSet<string> BlockedTypes { get; init; } = new();
    public FilterMode Mode { get; init; } = FilterMode.AllowAll;
}

public enum FilterMode
{
    AllowAll,
    AllowList,
    BlockList
}

public sealed class RateLimiterConfiguration
{
    public double MaxRateHz { get; init; } = 10.0;
    public bool PerSentenceType { get; init; } = false;
}

public sealed class LoggingConfiguration
{
    public bool Enabled { get; init; } = true;
    public string? LogDirectory { get; init; }
    public bool LogRawData { get; init; } = false;
}
