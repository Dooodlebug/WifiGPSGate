using System.Net;
using System.Net.Sockets;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;
using WifiGPSGate.IO.Sinks;
using WifiGPSGate.Tests.TestHelpers;

namespace WifiGPSGate.Tests.Integration;

public class UdpDataSinkTests : IAsyncLifetime
{
    private UdpClient? _receiver;
    private int _testPort;

    public async Task InitializeAsync()
    {
        // Find an available port
        using var tempSocket = new UdpClient(0);
        _testPort = ((IPEndPoint)tempSocket.Client.LocalEndPoint!).Port;
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _receiver?.Close();
        _receiver?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_TransitionsToConnected()
    {
        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "127.0.0.1",
            Port = _testPort,
            EnableBroadcast = false
        };
        await using var sink = new UdpDataSink(config);

        await sink.StartAsync();

        Assert.Equal(ConnectionState.Connected, sink.State);
        Assert.True(sink.IsReady);
    }

    [Fact]
    public async Task StopAsync_TransitionsToDisconnected()
    {
        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "127.0.0.1",
            Port = _testPort,
            EnableBroadcast = false
        };
        await using var sink = new UdpDataSink(config);
        await sink.StartAsync();

        await sink.StopAsync();

        Assert.Equal(ConnectionState.Disconnected, sink.State);
        Assert.False(sink.IsReady);
    }

    [Fact]
    public async Task WriteAsync_SendsDataToEndpoint()
    {
        // Start a receiver
        _receiver = new UdpClient(_testPort);
        var receivedData = new TaskCompletionSource<byte[]>();

        _ = Task.Run(async () =>
        {
            var result = await _receiver.ReceiveAsync();
            receivedData.SetResult(result.Buffer);
        });

        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "127.0.0.1",
            Port = _testPort,
            EnableBroadcast = false
        };
        await using var sink = new UdpDataSink(config);
        await sink.StartAsync();

        var data = System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGGA);
        await sink.WriteAsync(data);

        var received = await Task.WhenAny(receivedData.Task, Task.Delay(5000));

        Assert.True(receivedData.Task.IsCompleted, "Did not receive UDP data within timeout");
        var receivedBytes = await receivedData.Task;
        var receivedString = System.Text.Encoding.ASCII.GetString(receivedBytes);
        Assert.Contains("GNGGA", receivedString);
    }

    [Fact]
    public async Task WriteAsync_MultipleWrites_AllDataSent()
    {
        var receivedMessages = new List<string>();
        var messageCount = 0;
        var receivedAll = new TaskCompletionSource<bool>();

        _receiver = new UdpClient(_testPort);

        _ = Task.Run(async () =>
        {
            while (messageCount < 3)
            {
                var result = await _receiver.ReceiveAsync();
                receivedMessages.Add(System.Text.Encoding.ASCII.GetString(result.Buffer));
                messageCount++;
            }
            receivedAll.SetResult(true);
        });

        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "127.0.0.1",
            Port = _testPort,
            EnableBroadcast = false
        };
        await using var sink = new UdpDataSink(config);
        await sink.StartAsync();

        await sink.WriteAsync(System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGGA));
        await sink.WriteAsync(System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidRMC));
        await sink.WriteAsync(System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGSA));

        var completed = await Task.WhenAny(receivedAll.Task, Task.Delay(5000));

        Assert.True(receivedAll.Task.IsCompleted, "Did not receive all UDP messages within timeout");
        Assert.Equal(3, receivedMessages.Count);
    }

    [Fact]
    public async Task WriteAsync_WhenNotStarted_ThrowsException()
    {
        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "127.0.0.1",
            Port = _testPort,
            EnableBroadcast = false
        };
        await using var sink = new UdpDataSink(config);

        var data = System.Text.Encoding.ASCII.GetBytes(NmeaTestData.ValidGGA);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.WriteAsync(data).AsTask());
    }

    [Fact]
    public async Task StateChanged_EventFired_OnStateTransitions()
    {
        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "127.0.0.1",
            Port = _testPort,
            EnableBroadcast = false
        };
        await using var sink = new UdpDataSink(config);
        var stateChanges = new List<ConnectionState>();

        sink.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        await sink.StartAsync();
        await sink.StopAsync();

        Assert.Contains(ConnectionState.Connecting, stateChanges);
        Assert.Contains(ConnectionState.Connected, stateChanges);
        Assert.Contains(ConnectionState.Disconnected, stateChanges);
    }

    [Fact]
    public async Task Name_ReturnsAddressAndPort()
    {
        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "192.168.1.255",
            Port = 9002,
            EnableBroadcast = true
        };
        await using var sink = new UdpDataSink(config);

        Assert.Equal("UDP:192.168.1.255:9002", sink.Name);
    }

    [Fact]
    public async Task BroadcastEnabled_CanSendToBroadcastAddress()
    {
        var config = new UdpBroadcastOutputConfiguration
        {
            DestinationAddress = "255.255.255.255",
            Port = _testPort,
            EnableBroadcast = true
        };
        await using var sink = new UdpDataSink(config);

        // Should not throw
        await sink.StartAsync();
        Assert.True(sink.IsReady);
    }
}
