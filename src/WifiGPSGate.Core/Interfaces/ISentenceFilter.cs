using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Interfaces;

public interface ISentenceFilter
{
    bool IsAllowed(NmeaSentence sentence);
}
