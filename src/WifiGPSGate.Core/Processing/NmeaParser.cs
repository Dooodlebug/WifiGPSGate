using System.Buffers;
using System.Text;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;

namespace WifiGPSGate.Core.Processing;

public sealed class NmeaParser : INmeaParser
{
    private const byte StartDelimiter = (byte)'$';
    private const byte ChecksumDelimiter = (byte)'*';
    private const byte CarriageReturn = (byte)'\r';
    private const byte LineFeed = (byte)'\n';
    private const byte FieldSeparator = (byte)',';

    public NmeaSentence? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6) return null;

        // Find start delimiter
        int startIndex = data.IndexOf(StartDelimiter);
        if (startIndex < 0) return null;

        data = data.Slice(startIndex);

        // Find end of sentence (CR, LF, or end of data)
        int endIndex = FindEndOfSentence(data);
        if (endIndex < 0) endIndex = data.Length;

        var sentenceSpan = data.Slice(0, endIndex);

        return ParseSentence(sentenceSpan);
    }

    public IEnumerable<NmeaSentence> ParseStream(ReadOnlySpan<byte> data)
    {
        var sentences = new List<NmeaSentence>();
        int offset = 0;

        while (offset < data.Length)
        {
            var remaining = data.Slice(offset);

            // Find next start delimiter
            int startIndex = remaining.IndexOf(StartDelimiter);
            if (startIndex < 0) break;

            remaining = remaining.Slice(startIndex);
            offset += startIndex;

            // Find end of sentence
            int endIndex = FindEndOfSentence(remaining);
            if (endIndex < 0)
            {
                // Incomplete sentence, stop here
                break;
            }

            var sentenceSpan = remaining.Slice(0, endIndex);
            var sentence = ParseSentence(sentenceSpan);

            if (sentence != null)
            {
                sentences.Add(sentence);
            }

            offset += endIndex;

            // Skip line endings
            while (offset < data.Length && (data[offset] == CarriageReturn || data[offset] == LineFeed))
            {
                offset++;
            }
        }

        return sentences;
    }

    private static int FindEndOfSentence(ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == CarriageReturn || data[i] == LineFeed)
            {
                return i;
            }
        }
        return -1;
    }

    private static NmeaSentence? ParseSentence(ReadOnlySpan<byte> sentenceSpan)
    {
        if (sentenceSpan.Length < 6 || sentenceSpan[0] != StartDelimiter)
        {
            return null;
        }

        // Find checksum delimiter
        int checksumIndex = sentenceSpan.LastIndexOf(ChecksumDelimiter);
        byte providedChecksum = 0;
        byte calculatedChecksum = 0;
        bool hasValidChecksum = false;

        ReadOnlySpan<byte> dataSpan;
        if (checksumIndex > 0 && checksumIndex + 2 < sentenceSpan.Length)
        {
            dataSpan = sentenceSpan.Slice(1, checksumIndex - 1);
            var checksumHex = sentenceSpan.Slice(checksumIndex + 1, 2);

            if (TryParseHexByte(checksumHex, out providedChecksum))
            {
                calculatedChecksum = CalculateChecksum(dataSpan);
                hasValidChecksum = providedChecksum == calculatedChecksum;
            }
        }
        else
        {
            dataSpan = sentenceSpan.Slice(1);
            calculatedChecksum = CalculateChecksum(dataSpan);
        }

        // Parse talker ID and sentence type
        if (dataSpan.Length < 5)
        {
            return null;
        }

        // NMEA sentences typically have 2-char talker ID and 3-char sentence type
        // e.g., GPGGA, GNRMC, etc.
        string talkerId = Encoding.ASCII.GetString(dataSpan.Slice(0, 2));

        // Find first comma to get sentence type
        int firstComma = dataSpan.IndexOf(FieldSeparator);
        if (firstComma < 3)
        {
            return null;
        }

        string sentenceType = Encoding.ASCII.GetString(dataSpan.Slice(2, firstComma - 2));

        // Parse fields
        var fieldsSpan = dataSpan.Slice(firstComma + 1);
        var fields = ParseFields(fieldsSpan);

        // Create raw data copy
        var rawData = new byte[sentenceSpan.Length];
        sentenceSpan.CopyTo(rawData);

        return new NmeaSentence
        {
            TalkerId = talkerId,
            SentenceType = sentenceType,
            Fields = fields,
            Checksum = checksumIndex > 0 ? providedChecksum : calculatedChecksum,
            RawData = rawData,
            IsValid = hasValidChecksum
        };
    }

    private static string[] ParseFields(ReadOnlySpan<byte> data)
    {
        var fields = new List<string>();
        int start = 0;

        for (int i = 0; i <= data.Length; i++)
        {
            if (i == data.Length || data[i] == FieldSeparator)
            {
                var fieldSpan = data.Slice(start, i - start);
                fields.Add(Encoding.ASCII.GetString(fieldSpan));
                start = i + 1;
            }
        }

        return fields.ToArray();
    }

    private static byte CalculateChecksum(ReadOnlySpan<byte> data)
    {
        byte checksum = 0;
        foreach (byte b in data)
        {
            checksum ^= b;
        }
        return checksum;
    }

    private static bool TryParseHexByte(ReadOnlySpan<byte> hex, out byte result)
    {
        result = 0;
        if (hex.Length != 2) return false;

        int high = HexCharToNibble((char)hex[0]);
        int low = HexCharToNibble((char)hex[1]);

        if (high < 0 || low < 0) return false;

        result = (byte)((high << 4) | low);
        return true;
    }

    private static int HexCharToNibble(char c)
    {
        return c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'F' => c - 'A' + 10,
            >= 'a' and <= 'f' => c - 'a' + 10,
            _ => -1
        };
    }
}
