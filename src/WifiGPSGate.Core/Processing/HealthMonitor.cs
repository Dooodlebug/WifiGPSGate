using System.Diagnostics;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Processing;

public sealed class HealthMonitor : IHealthMonitor, IDisposable
{
    private readonly TimeSpan _staleThreshold;
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _recentTimestamps = new();
    private readonly Timer _checkTimer;

    private HealthStatus _currentStatus = HealthStatus.Unknown;
    private DateTimeOffset _lastDataReceived = DateTimeOffset.MinValue;

    public HealthStatus CurrentStatus
    {
        get
        {
            lock (_lock)
            {
                return _currentStatus;
            }
        }
    }

    public DateTimeOffset LastDataReceived
    {
        get
        {
            lock (_lock)
            {
                return _lastDataReceived;
            }
        }
    }

    public double DataRateHz
    {
        get
        {
            lock (_lock)
            {
                return CalculateDataRate();
            }
        }
    }

    public event EventHandler<HealthChangedEventArgs>? HealthChanged;

    public HealthMonitor(TimeSpan? staleThreshold = null)
    {
        _staleThreshold = staleThreshold ?? TimeSpan.FromSeconds(3);
        _checkTimer = new Timer(CheckHealth, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    public void RecordSentence(NmeaSentence sentence)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            _lastDataReceived = now;
            _recentTimestamps.Enqueue(now);

            // Keep only last 2 seconds of timestamps for rate calculation
            var cutoff = now.AddSeconds(-2);
            while (_recentTimestamps.Count > 0 && _recentTimestamps.Peek() < cutoff)
            {
                _recentTimestamps.Dequeue();
            }

            UpdateStatus(HealthStatus.Healthy);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _recentTimestamps.Clear();
            _lastDataReceived = DateTimeOffset.MinValue;
            UpdateStatus(HealthStatus.Unknown);
        }
    }

    private void CheckHealth(object? state)
    {
        lock (_lock)
        {
            if (_currentStatus == HealthStatus.Unknown || _lastDataReceived == DateTimeOffset.MinValue)
            {
                return;
            }

            var timeSinceLastData = DateTimeOffset.UtcNow - _lastDataReceived;
            if (timeSinceLastData > _staleThreshold)
            {
                UpdateStatus(HealthStatus.Stale);
            }
        }
    }

    private void UpdateStatus(HealthStatus newStatus)
    {
        if (_currentStatus != newStatus)
        {
            var oldStatus = _currentStatus;
            _currentStatus = newStatus;

            HealthChanged?.Invoke(this, new HealthChangedEventArgs
            {
                OldStatus = oldStatus,
                NewStatus = newStatus
            });
        }
    }

    private double CalculateDataRate()
    {
        if (_recentTimestamps.Count < 2) return 0;

        var oldest = _recentTimestamps.Peek();
        var newest = _lastDataReceived;
        var duration = (newest - oldest).TotalSeconds;

        if (duration <= 0) return 0;

        return (_recentTimestamps.Count - 1) / duration;
    }

    public void Dispose()
    {
        _checkTimer.Dispose();
    }
}
