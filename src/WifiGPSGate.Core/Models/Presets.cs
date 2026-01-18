namespace WifiGPSGate.Core.Models;

public static class Presets
{
    public static SessionConfiguration CreateEmlidPreset(string serialPortName)
    {
        return new SessionConfiguration
        {
            Input = new UdpInputConfiguration
            {
                Port = 9001
            },
            Outputs = new List<OutputConfiguration>
            {
                new SerialOutputConfiguration
                {
                    PortName = serialPortName,
                    BaudRate = 115200
                }
            },
            Filter = new SentenceFilterConfiguration
            {
                Mode = FilterMode.AllowAll
            }
        };
    }

    public static readonly string[] CommonNmeaTypes = new[]
    {
        "GGA", "RMC", "GSA", "GSV", "GLL", "VTG", "ZDA", "GNS", "GST"
    };

    public static readonly string[] CommonTalkerIds = new[]
    {
        "GP", "GN", "GL", "GA", "GB", "GQ"
    };

    public static readonly int[] CommonBaudRates = new[]
    {
        4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800
    };
}
