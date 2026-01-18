using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Interfaces;

public interface INmeaParser
{
    NmeaSentence? TryParse(ReadOnlySpan<byte> data);
    IEnumerable<NmeaSentence> ParseStream(ReadOnlySpan<byte> data);
}
