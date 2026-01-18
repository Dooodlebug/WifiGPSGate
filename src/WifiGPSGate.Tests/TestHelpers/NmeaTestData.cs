namespace WifiGPSGate.Tests.TestHelpers;

public static class NmeaTestData
{
    // Valid NMEA sentences with correct checksums
    public const string ValidGGA = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*51\r\n";
    public const string ValidRMC = "$GNRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*74\r\n";
    public const string ValidGSA = "$GNGSA,A,3,04,05,,09,12,,,24,,,,,2.5,1.3,2.1*27\r\n";
    public const string ValidGSV = "$GPGSV,2,1,08,01,40,083,46,02,17,308,41,12,07,344,39,14,22,228,45*75\r\n";
    public const string ValidVTG = "$GNVTG,054.7,T,034.4,M,005.5,N,010.2,K*56\r\n";

    // Invalid sentences
    public const string InvalidChecksum = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*99\r\n";
    public const string MalformedNoStart = "GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*47\r\n";
    public const string TooShort = "$GP\r\n";

    // Multiple sentences
    public static readonly string MultipleValidSentences = ValidGGA + ValidRMC + ValidGSA;

    // Generates a batch of sentences for throughput testing
    public static string GenerateBatch(int count)
    {
        var sentences = new string[] { ValidGGA, ValidRMC, ValidGSA, ValidVTG };
        var result = new System.Text.StringBuilder();

        for (int i = 0; i < count; i++)
        {
            result.Append(sentences[i % sentences.Length]);
        }

        return result.ToString();
    }

    // Generates bytes for a batch of sentences
    public static byte[] GenerateBatchBytes(int count)
    {
        return System.Text.Encoding.ASCII.GetBytes(GenerateBatch(count));
    }

    // Simulated GPS position updates (different timestamps)
    public static string[] GeneratePositionSequence(int count)
    {
        var result = new string[count];
        var baseTime = new TimeSpan(12, 35, 19);

        for (int i = 0; i < count; i++)
        {
            var time = baseTime.Add(TimeSpan.FromSeconds(i));
            var timeStr = $"{time.Hours:D2}{time.Minutes:D2}{time.Seconds:D2}";

            // Calculate checksum for the sentence
            var sentence = $"GNGGA,{timeStr},4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,";
            var checksum = CalculateChecksum(sentence);
            result[i] = $"${sentence}*{checksum:X2}\r\n";
        }

        return result;
    }

    private static byte CalculateChecksum(string sentence)
    {
        byte checksum = 0;
        foreach (char c in sentence)
        {
            checksum ^= (byte)c;
        }
        return checksum;
    }
}
