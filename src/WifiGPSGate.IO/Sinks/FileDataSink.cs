using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.IO.Sinks;

public sealed class FileDataSink : IDataSink
{
    private readonly FileOutputConfiguration _config;
    private readonly ILogger _logger;
    private StreamWriter? _writer;
    private FileStream? _fileStream;
    private ConnectionState _state = ConnectionState.Disconnected;
    private string? _actualFilePath;

    public string Name => $"File:{Path.GetFileName(_config.FilePath)}";
    public ConnectionState State => _state;
    public bool IsReady => _state == ConnectionState.Connected && _writer != null;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public FileDataSink(FileOutputConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger?.ForContext<FileDataSink>() ?? Log.Logger.ForContext<FileDataSink>();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected)
        {
            return Task.CompletedTask;
        }

        SetState(ConnectionState.Connecting);

        try
        {
            _actualFilePath = GetActualFilePath();
            var directory = Path.GetDirectoryName(_actualFilePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _fileStream = new FileStream(_actualFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(_fileStream) { AutoFlush = true };

            SetState(ConnectionState.Connected);
            _logger.Information("File logging started: {FilePath}", _actualFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open log file {FilePath}", _config.FilePath);
            SetState(ConnectionState.Error, ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Disconnected)
        {
            return Task.CompletedTask;
        }

        try
        {
            _writer?.Flush();
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;

            _fileStream?.Close();
            _fileStream?.Dispose();
            _fileStream = null;

            SetState(ConnectionState.Disconnected);
            _logger.Information("File logging stopped: {FilePath}", _actualFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error closing log file {FilePath}", _actualFilePath);
        }

        return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!IsReady || _writer == null)
        {
            throw new InvalidOperationException("File sink is not ready");
        }

        try
        {
            var text = System.Text.Encoding.ASCII.GetString(data.Span);
            await _writer.WriteAsync(text);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to log file {FilePath}", _actualFilePath);
            SetState(ConnectionState.Error, ex.Message);
            throw;
        }
    }

    private string GetActualFilePath()
    {
        if (!_config.AppendTimestamp)
        {
            return _config.FilePath;
        }

        var directory = Path.GetDirectoryName(_config.FilePath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(_config.FilePath);
        var extension = Path.GetExtension(_config.FilePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        return Path.Combine(directory, $"{fileName}_{timestamp}{extension}");
    }

    private void SetState(ConnectionState newState, string? errorMessage = null)
    {
        var oldState = _state;
        if (oldState != newState)
        {
            _state = newState;
            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
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
