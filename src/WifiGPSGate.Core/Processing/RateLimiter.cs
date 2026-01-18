using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Processing;

public sealed class RateLimiter : IRateLimiter
{
    private readonly RateLimiterConfiguration _config;
    private readonly Dictionary<string, DateTimeOffset> _lastEmitTimes = new();
    private DateTimeOffset _globalLastEmitTime = DateTimeOffset.MinValue;
    private readonly object _lock = new();

    public RateLimiter(RateLimiterConfiguration? config = null)
    {
        _config = config ?? new RateLimiterConfiguration { MaxRateHz = 10.0 };
    }

    public bool ShouldEmit(NmeaSentence sentence)
    {
        if (_config.MaxRateHz <= 0) return true;

        var minInterval = TimeSpan.FromSeconds(1.0 / _config.MaxRateHz);
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (_config.PerSentenceType)
            {
                var key = sentence.FullType;
                if (_lastEmitTimes.TryGetValue(key, out var lastTime))
                {
                    if (now - lastTime < minInterval)
                    {
                        return false;
                    }
                }
                _lastEmitTimes[key] = now;
                return true;
            }
            else
            {
                if (now - _globalLastEmitTime < minInterval)
                {
                    return false;
                }
                _globalLastEmitTime = now;
                return true;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _lastEmitTimes.Clear();
            _globalLastEmitTime = DateTimeOffset.MinValue;
        }
    }
}
