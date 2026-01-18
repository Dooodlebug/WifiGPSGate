using System.Text;
using WifiGPSGate.Core.Processing;

namespace WifiGPSGate.Tests;

public class NmeaParserTests
{
    private readonly NmeaParser _parser = new();

    [Fact]
    public void TryParse_ValidGgaSentence_ParsesCorrectly()
    {
        // Arrange
        var sentence = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*51\r\n";
        var data = Encoding.ASCII.GetBytes(sentence);

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GN", result.TalkerId);
        Assert.Equal("GGA", result.SentenceType);
        Assert.Equal("GNGGA", result.FullType);
        Assert.True(result.IsValid);
        Assert.Equal(0x51, result.Checksum);
    }

    [Fact]
    public void TryParse_ValidRmcSentence_ParsesCorrectly()
    {
        // Arrange
        var sentence = "$GNRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*74\r\n";
        var data = Encoding.ASCII.GetBytes(sentence);

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GN", result.TalkerId);
        Assert.Equal("RMC", result.SentenceType);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void TryParse_InvalidChecksum_MarksAsInvalid()
    {
        // Arrange - checksum should be 51, but we use 99
        var sentence = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*99\r\n";
        var data = Encoding.ASCII.GetBytes(sentence);

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void TryParse_TooShortData_ReturnsNull()
    {
        // Arrange
        var data = Encoding.ASCII.GetBytes("$GP");

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_NoStartDelimiter_ReturnsNull()
    {
        // Arrange
        var data = Encoding.ASCII.GetBytes("GNGGA,123519\r\n");

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseStream_MultipleSentences_ParsesAll()
    {
        // Arrange
        var data = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*51\r\n" +
                   "$GNRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*74\r\n";
        var bytes = Encoding.ASCII.GetBytes(data);

        // Act
        var results = _parser.ParseStream(bytes).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("GGA", results[0].SentenceType);
        Assert.Equal("RMC", results[1].SentenceType);
    }

    [Fact]
    public void TryParse_ExtractsFieldsCorrectly()
    {
        // Arrange
        var sentence = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*51\r\n";
        var data = Encoding.ASCII.GetBytes(sentence);

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123519", result.Fields[0]); // Time
        Assert.Equal("4807.038", result.Fields[1]); // Latitude
        Assert.Equal("N", result.Fields[2]); // N/S
        Assert.Equal("01131.000", result.Fields[3]); // Longitude
        Assert.Equal("E", result.Fields[4]); // E/W
    }

    [Fact]
    public void TryParse_GpTalkerId_ParsesCorrectly()
    {
        // Arrange
        var sentence = "$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*4F\r\n";
        var data = Encoding.ASCII.GetBytes(sentence);

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GP", result.TalkerId);
        Assert.Equal("GGA", result.SentenceType);
    }

    [Fact]
    public void ToString_ReturnsOriginalSentence()
    {
        // Arrange
        var sentence = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*51";
        var data = Encoding.ASCII.GetBytes(sentence + "\r\n");

        // Act
        var result = _parser.TryParse(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sentence, result.ToString());
    }
}
