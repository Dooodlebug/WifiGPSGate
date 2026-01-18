using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Processing;

public sealed class SentenceFilter : ISentenceFilter
{
    private readonly SentenceFilterConfiguration _config;

    public SentenceFilter(SentenceFilterConfiguration? config = null)
    {
        _config = config ?? new SentenceFilterConfiguration { Mode = FilterMode.AllowAll };
    }

    public bool IsAllowed(NmeaSentence sentence)
    {
        return _config.Mode switch
        {
            FilterMode.AllowAll => true,
            FilterMode.AllowList => IsInAllowList(sentence),
            FilterMode.BlockList => !IsInBlockList(sentence),
            _ => true
        };
    }

    private bool IsInAllowList(NmeaSentence sentence)
    {
        if (_config.AllowedTypes.Count == 0) return true;

        // Check full type (e.g., "GPGGA")
        if (_config.AllowedTypes.Contains(sentence.FullType)) return true;

        // Check sentence type only (e.g., "GGA")
        if (_config.AllowedTypes.Contains(sentence.SentenceType)) return true;

        return false;
    }

    private bool IsInBlockList(NmeaSentence sentence)
    {
        // Check full type (e.g., "GPGGA")
        if (_config.BlockedTypes.Contains(sentence.FullType)) return true;

        // Check sentence type only (e.g., "GGA")
        if (_config.BlockedTypes.Contains(sentence.SentenceType)) return true;

        return false;
    }
}
