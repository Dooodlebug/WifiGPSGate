namespace WifiGPSGate.Core.Models;

public sealed class NmeaSentence
{
    public required string TalkerId { get; init; }
    public required string SentenceType { get; init; }
    public required string[] Fields { get; init; }
    public required byte Checksum { get; init; }
    public required ReadOnlyMemory<byte> RawData { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool IsValid { get; init; }

    public string FullType => $"{TalkerId}{SentenceType}";

    public override string ToString()
    {
        return System.Text.Encoding.ASCII.GetString(RawData.Span).TrimEnd('\r', '\n');
    }
}
