using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;
using WifiGPSGate.IO.Sinks;
using WifiGPSGate.Tests.TestHelpers;

namespace WifiGPSGate.Tests.Integration;

public class FileDataSinkTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<string> _createdFiles = new();

    public FileDataSinkTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"WifiGPSGate_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        foreach (var file in _createdFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private string CreateTestFilePath(string name = "test.nmea")
    {
        var path = Path.Combine(_testDir, name);
        _createdFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task StartAsync_CreatesFileAndConnects()
    {
        var filePath = CreateTestFilePath();
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);

        await sink.StartAsync();

        Assert.Equal(ConnectionState.Connected, sink.State);
        Assert.True(sink.IsReady);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task StartAsync_CreatesDirectoryIfNotExists()
    {
        var filePath = Path.Combine(_testDir, "subdir", "test.nmea");
        _createdFiles.Add(filePath);
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);

        await sink.StartAsync();

        Assert.True(Directory.Exists(Path.GetDirectoryName(filePath)));
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task WriteAsync_WritesDataToFile()
    {
        var filePath = CreateTestFilePath();
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);
        await sink.StartAsync();

        var data = System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGGA);
        await sink.WriteAsync(data);
        await sink.StopAsync();

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("GNGGA", content);
    }

    [Fact]
    public async Task WriteAsync_MultipleWrites_AllDataWritten()
    {
        var filePath = CreateTestFilePath();
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);
        await sink.StartAsync();

        await sink.WriteAsync(System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGGA));
        await sink.WriteAsync(System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidRMC));
        await sink.WriteAsync(System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGSA));
        await sink.StopAsync();

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("GNGGA", content);
        Assert.Contains("GNRMC", content);
        Assert.Contains("GNGSA", content);
    }

    [Fact]
    public async Task StopAsync_ClosesFileAndDisconnects()
    {
        var filePath = CreateTestFilePath();
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);
        await sink.StartAsync();

        await sink.StopAsync();

        Assert.Equal(ConnectionState.Disconnected, sink.State);
        Assert.False(sink.IsReady);
    }

    [Fact]
    public async Task WriteAsync_WhenNotStarted_ThrowsException()
    {
        var filePath = CreateTestFilePath();
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);

        var data = System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGGA);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.WriteAsync(data).AsTask());
    }

    [Fact]
    public async Task AppendTimestamp_True_CreatesTimestampedFile()
    {
        var filePath = CreateTestFilePath("test.nmea");
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = true
        };
        await using var sink = new FileDataSink(config);
        await sink.StartAsync();
        await sink.StopAsync();

        // The actual file should have a timestamp, not be the original path
        Assert.False(File.Exists(filePath));

        // Find the timestamped file
        var files = Directory.GetFiles(_testDir, "test_*.nmea");
        Assert.Single(files);
        _createdFiles.Add(files[0]);
    }

    [Fact]
    public async Task StateChanged_EventFired_OnStateTransitions()
    {
        var filePath = CreateTestFilePath();
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);
        var stateChanges = new List<ConnectionState>();

        sink.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        await sink.StartAsync();
        await sink.StopAsync();

        Assert.Contains(ConnectionState.Connecting, stateChanges);
        Assert.Contains(ConnectionState.Connected, stateChanges);
        Assert.Contains(ConnectionState.Disconnected, stateChanges);
    }

    [Fact]
    public async Task Name_ReturnsFileNameWithPrefix()
    {
        var filePath = CreateTestFilePath("mylog.nmea");
        var config = new FileOutputConfiguration
        {
            FilePath = filePath,
            AppendTimestamp = false
        };
        await using var sink = new FileDataSink(config);

        Assert.Equal("File:mylog.nmea", sink.Name);
    }
}
