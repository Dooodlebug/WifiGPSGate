using System.Text;
using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;
using WifiGPSGate.Core.Processing;

namespace WifiGPSGate.Core.Orchestration;

public sealed class SessionManager : ISessionManager
{
    private readonly ILogger _logger;
    private readonly Func<InputConfiguration, IDataSource> _sourceFactory;
    private readonly Func<OutputConfiguration, IDataSink> _sinkFactory;

    private IDataSource? _source;
    private readonly List<IDataSink> _sinks = new();
    private INmeaParser? _parser;
    private ISentenceFilter? _filter;
    private IRateLimiter? _rateLimiter;
    private HealthMonitor? _healthMonitor;
    private SessionConfiguration? _config;

    private SessionState _state = SessionState.Stopped;
    private readonly SessionStatistics _statistics = new();
    private readonly object _lock = new();

    public SessionState State => _state;
    public SessionStatistics Statistics => _statistics;

    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;
    public event EventHandler<NmeaSentence>? SentenceReceived;

    public SessionManager(
        Func<InputConfiguration, IDataSource> sourceFactory,
        Func<OutputConfiguration, IDataSink> sinkFactory,
        ILogger? logger = null)
    {
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _sinkFactory = sinkFactory ?? throw new ArgumentNullException(nameof(sinkFactory));
        _logger = logger?.ForContext<SessionManager>() ?? Log.Logger.ForContext<SessionManager>();
    }

    public async Task StartAsync(SessionConfiguration config, CancellationToken ct = default)
    {
        if (_state != SessionState.Stopped)
        {
            throw new InvalidOperationException($"Cannot start session in state {_state}");
        }

        SetState(SessionState.Starting);
        _config = config;
        _statistics.Reset();
        _statistics.SessionStartTime = DateTimeOffset.UtcNow;

        try
        {
            // Initialize components
            _parser = new NmeaParser();
            _filter = config.Filter != null ? new SentenceFilter(config.Filter) : new SentenceFilter();
            _rateLimiter = config.RateLimiter != null ? new RateLimiter(config.RateLimiter) : null;
            _healthMonitor = new HealthMonitor();

            // Create and start source
            _source = _sourceFactory(config.Input);
            _source.DataReceived += OnDataReceived;
            _source.StateChanged += OnSourceStateChanged;
            await _source.StartAsync(ct);

            // Create and start sinks
            foreach (var outputConfig in config.Outputs.Where(o => o.Enabled))
            {
                var sink = _sinkFactory(outputConfig);
                sink.StateChanged += OnSinkStateChanged;
                await sink.StartAsync(ct);
                _sinks.Add(sink);
            }

            SetState(SessionState.Running);
            _logger.Information("Session started with {SourceName} -> {SinkCount} outputs",
                _source.Name, _sinks.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start session");
            SetState(SessionState.Error, ex.Message);
            await StopAsync(ct);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_state == SessionState.Stopped)
        {
            return;
        }

        SetState(SessionState.Stopping);

        // Stop source
        if (_source != null)
        {
            _source.DataReceived -= OnDataReceived;
            _source.StateChanged -= OnSourceStateChanged;
            await _source.StopAsync(ct);
            await _source.DisposeAsync();
            _source = null;
        }

        // Stop sinks
        foreach (var sink in _sinks)
        {
            sink.StateChanged -= OnSinkStateChanged;
            await sink.StopAsync(ct);
            await sink.DisposeAsync();
        }
        _sinks.Clear();

        // Cleanup components
        _healthMonitor?.Dispose();
        _healthMonitor = null;
        _parser = null;
        _filter = null;
        _rateLimiter = null;

        SetState(SessionState.Stopped);
        _logger.Information("Session stopped");
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            _statistics.BytesReceived += e.Data.Length;
            _statistics.LastDataReceivedTime = e.Timestamp;

            if (_parser == null) return;

            var sentences = _parser.ParseStream(e.Data.Span);

            foreach (var sentence in sentences)
            {
                ProcessSentence(sentence);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing received data");
            _statistics.ParseErrors++;
        }
    }

    private void ProcessSentence(NmeaSentence sentence)
    {
        _statistics.SentencesReceived++;

        if (!sentence.IsValid)
        {
            _statistics.ChecksumErrors++;
            _logger.Debug("Invalid checksum for sentence {Type}", sentence.FullType);
            return;
        }

        // Apply filter
        if (_filter != null && !_filter.IsAllowed(sentence))
        {
            return;
        }

        // Apply rate limiter
        if (_rateLimiter != null && !_rateLimiter.ShouldEmit(sentence))
        {
            return;
        }

        // Record for health monitoring
        _healthMonitor?.RecordSentence(sentence);
        _statistics.CurrentRateHz = _healthMonitor?.DataRateHz ?? 0;

        // Notify subscribers
        SentenceReceived?.Invoke(this, sentence);

        // Write to all sinks
        WriteToSinks(sentence);
    }

    private void WriteToSinks(NmeaSentence sentence)
    {
        // Ensure sentence has line ending
        var rawData = sentence.RawData;
        var dataSpan = rawData.Span;

        ReadOnlyMemory<byte> dataToWrite;
        if (dataSpan.Length >= 2 && dataSpan[^2] == '\r' && dataSpan[^1] == '\n')
        {
            dataToWrite = rawData;
        }
        else if (dataSpan.Length >= 1 && (dataSpan[^1] == '\r' || dataSpan[^1] == '\n'))
        {
            var withEnding = new byte[rawData.Length + 1];
            rawData.CopyTo(withEnding);
            withEnding[^1] = (byte)'\n';
            if (dataSpan[^1] == '\r')
            {
                // Already has \r, just add \n
            }
            else
            {
                // Has \n, need full \r\n
                withEnding = new byte[rawData.Length + 1];
                rawData.CopyTo(withEnding);
                withEnding[rawData.Length - 1] = (byte)'\r';
                withEnding[rawData.Length] = (byte)'\n';
            }
            dataToWrite = withEnding;
        }
        else
        {
            var withEnding = new byte[rawData.Length + 2];
            rawData.CopyTo(withEnding);
            withEnding[^2] = (byte)'\r';
            withEnding[^1] = (byte)'\n';
            dataToWrite = withEnding;
        }

        foreach (var sink in _sinks)
        {
            if (sink.IsReady)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await sink.WriteAsync(dataToWrite);
                        _statistics.SentencesSent++;
                        _statistics.BytesSent += dataToWrite.Length;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error writing to sink {SinkName}", sink.Name);
                        _statistics.WriteErrors++;
                    }
                });
            }
        }
    }

    private void OnSourceStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _logger.Information("Source state changed: {OldState} -> {NewState}", e.OldState, e.NewState);

        if (e.NewState == ConnectionState.Error && _state == SessionState.Running)
        {
            SetState(SessionState.Error, e.ErrorMessage);
        }
    }

    private void OnSinkStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (sender is IDataSink sink)
        {
            _logger.Information("Sink {Name} state changed: {OldState} -> {NewState}",
                sink.Name, e.OldState, e.NewState);
        }
    }

    private void SetState(SessionState newState, string? errorMessage = null)
    {
        var oldState = _state;
        if (oldState != newState)
        {
            _state = newState;
            StateChanged?.Invoke(this, new SessionStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                ErrorMessage = errorMessage
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
