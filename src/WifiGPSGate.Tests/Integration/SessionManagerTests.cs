using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;
using WifiGPSGate.Core.Orchestration;
using WifiGPSGate.Tests.Mocks;
using WifiGPSGate.Tests.TestHelpers;

namespace WifiGPSGate.Tests.Integration;

public class SessionManagerTests
{
    private MockDataSource _source = null!;
    private MockDataSink _sink = null!;
    private SessionManager _manager = null!;

    private void SetupManager()
    {
        _source = new MockDataSource();
        _sink = new MockDataSink();

        _manager = new SessionManager(
            _ => _source,
            _ => _sink);
    }

    private SessionConfiguration CreateBasicConfig()
    {
        return new SessionConfiguration
        {
            Input = new UdpInputConfiguration { Port = 9001 },
            Outputs = new List<OutputConfiguration>
            {
                new SerialOutputConfiguration { PortName = "COM1" }
            },
            Filter = new SentenceFilterConfiguration { Mode = FilterMode.AllowAll }
        };
    }

    [Fact]
    public async Task StartAsync_TransitionsToRunningState()
    {
        SetupManager();
        var config = CreateBasicConfig();

        await _manager.StartAsync(config);

        Assert.Equal(SessionState.Running, _manager.State);
    }

    [Fact]
    public async Task StopAsync_TransitionsToStoppedState()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        await _manager.StopAsync();

        Assert.Equal(SessionState.Stopped, _manager.State);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ThrowsException()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.StartAsync(config));
    }

    [Fact]
    public async Task DataFlow_ValidSentence_RoutesToSink()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.ValidGGA);

        // Allow time for async processing
        await Task.Delay(100);

        Assert.True(_sink.WriteCount > 0);
        var written = _sink.GetWrittenDataAsString(0);
        Assert.Contains("GNGGA", written);
    }

    [Fact]
    public async Task DataFlow_InvalidChecksum_NotRoutedToSink()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.InvalidChecksum);

        await Task.Delay(100);

        Assert.Equal(0, _sink.WriteCount);
    }

    [Fact]
    public async Task DataFlow_MultipleSentences_AllRouted()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.MultipleValidSentences);

        await Task.Delay(200);

        Assert.Equal(3, _sink.WriteCount);
    }

    [Fact]
    public async Task Statistics_TracksReceivedSentences()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.ValidGGA);
        _source.EmitData(NmeaTestData.ValidRMC);

        await Task.Delay(100);

        Assert.True(_manager.Statistics.SentencesReceived >= 2);
    }

    [Fact]
    public async Task Statistics_TracksBytesReceived()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        var data = NmeaTestData.ValidGGA;
        _source.EmitData(data);

        await Task.Delay(100);

        Assert.Equal(data.Length, _manager.Statistics.BytesReceived);
    }

    [Fact]
    public async Task Statistics_TracksChecksumErrors()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.InvalidChecksum);

        await Task.Delay(100);

        Assert.Equal(1, _manager.Statistics.ChecksumErrors);
    }

    [Fact]
    public async Task Filter_AllowList_OnlyAllowedTypesPass()
    {
        SetupManager();
        var config = new SessionConfiguration
        {
            Input = new UdpInputConfiguration { Port = 9001 },
            Outputs = new List<OutputConfiguration>
            {
                new SerialOutputConfiguration { PortName = "COM1" }
            },
            Filter = new SentenceFilterConfiguration
            {
                Mode = FilterMode.AllowList,
                AllowedTypes = new HashSet<string> { "GGA" }
            }
        };
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.ValidGGA);
        _source.EmitData(NmeaTestData.ValidRMC);

        await Task.Delay(100);

        Assert.Equal(1, _sink.WriteCount);
        var written = _sink.GetWrittenDataAsString(0);
        Assert.Contains("GGA", written);
    }

    [Fact]
    public async Task Filter_BlockList_BlockedTypesFiltered()
    {
        SetupManager();
        var config = new SessionConfiguration
        {
            Input = new UdpInputConfiguration { Port = 9001 },
            Outputs = new List<OutputConfiguration>
            {
                new SerialOutputConfiguration { PortName = "COM1" }
            },
            Filter = new SentenceFilterConfiguration
            {
                Mode = FilterMode.BlockList,
                BlockedTypes = new HashSet<string> { "RMC" }
            }
        };
        await _manager.StartAsync(config);

        _source.EmitData(NmeaTestData.ValidGGA);
        _source.EmitData(NmeaTestData.ValidRMC);

        await Task.Delay(100);

        Assert.Equal(1, _sink.WriteCount);
        var written = _sink.GetWrittenDataAsString(0);
        Assert.Contains("GGA", written);
    }

    [Fact]
    public async Task StateChanged_EventFired_OnStateTransitions()
    {
        SetupManager();
        var config = CreateBasicConfig();
        var stateChanges = new List<SessionState>();

        _manager.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        await _manager.StartAsync(config);
        await _manager.StopAsync();

        Assert.Contains(SessionState.Starting, stateChanges);
        Assert.Contains(SessionState.Running, stateChanges);
        Assert.Contains(SessionState.Stopping, stateChanges);
        Assert.Contains(SessionState.Stopped, stateChanges);
    }

    [Fact]
    public async Task SentenceReceived_EventFired_ForValidSentences()
    {
        SetupManager();
        var config = CreateBasicConfig();
        var receivedSentences = new List<NmeaSentence>();

        _manager.SentenceReceived += (s, sentence) => receivedSentences.Add(sentence);

        await _manager.StartAsync(config);
        _source.EmitData(NmeaTestData.ValidGGA);

        await Task.Delay(100);

        Assert.Single(receivedSentences);
        Assert.Equal("GGA", receivedSentences[0].SentenceType);
    }

    [Fact]
    public async Task DisposeAsync_StopsSession()
    {
        SetupManager();
        var config = CreateBasicConfig();
        await _manager.StartAsync(config);

        await _manager.DisposeAsync();

        Assert.Equal(SessionState.Stopped, _manager.State);
    }
}
