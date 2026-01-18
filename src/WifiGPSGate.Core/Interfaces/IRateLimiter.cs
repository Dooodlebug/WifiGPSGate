using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Interfaces;

public interface IRateLimiter
{
    bool ShouldEmit(NmeaSentence sentence);
    void Reset();
}
