namespace WifiGPSGate.Core.Models;

public sealed class SessionStatistics
{
    public long SentencesReceived { get; set; }
    public long SentencesSent { get; set; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public long ParseErrors { get; set; }
    public long ChecksumErrors { get; set; }
    public long WriteErrors { get; set; }
    public DateTimeOffset? SessionStartTime { get; set; }
    public DateTimeOffset? LastDataReceivedTime { get; set; }
    public double CurrentRateHz { get; set; }

    public TimeSpan SessionDuration => SessionStartTime.HasValue
        ? DateTimeOffset.UtcNow - SessionStartTime.Value
        : TimeSpan.Zero;

    public void Reset()
    {
        SentencesReceived = 0;
        SentencesSent = 0;
        BytesReceived = 0;
        BytesSent = 0;
        ParseErrors = 0;
        ChecksumErrors = 0;
        WriteErrors = 0;
        SessionStartTime = null;
        LastDataReceivedTime = null;
        CurrentRateHz = 0;
    }
}
