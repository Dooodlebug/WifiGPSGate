using System.Text;
using WifiGPSGate.Core.Models;
using WifiGPSGate.Core.Processing;

namespace WifiGPSGate.Tests;

public class SentenceFilterTests
{
    private readonly NmeaParser _parser = new();

    private NmeaSentence CreateSentence(string type)
    {
        var sentence = type switch
        {
            "GNGGA" => "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*47\r\n",
            "GNRMC" => "$GNRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A\r\n",
            "GNGSA" => "$GNGSA,A,3,01,02,03,04,05,06,07,08,09,10,11,12,1.0,1.0,1.0*2E\r\n",
            "GPGSV" => "$GPGSV,3,1,11,01,05,081,20,02,09,135,15,03,17,189,24,04,21,300,30*73\r\n",
            _ => throw new ArgumentException($"Unknown type: {type}")
        };
        return _parser.TryParse(Encoding.ASCII.GetBytes(sentence))!;
    }

    [Fact]
    public void AllowAll_AllowsEverything()
    {
        // Arrange
        var config = new SentenceFilterConfiguration { Mode = FilterMode.AllowAll };
        var filter = new SentenceFilter(config);
        var gga = CreateSentence("GNGGA");
        var rmc = CreateSentence("GNRMC");

        // Act & Assert
        Assert.True(filter.IsAllowed(gga));
        Assert.True(filter.IsAllowed(rmc));
    }

    [Fact]
    public void AllowList_AllowsOnlySpecifiedTypes()
    {
        // Arrange
        var config = new SentenceFilterConfiguration
        {
            Mode = FilterMode.AllowList,
            AllowedTypes = new HashSet<string> { "GGA", "RMC" }
        };
        var filter = new SentenceFilter(config);
        var gga = CreateSentence("GNGGA");
        var rmc = CreateSentence("GNRMC");
        var gsa = CreateSentence("GNGSA");

        // Act & Assert
        Assert.True(filter.IsAllowed(gga));
        Assert.True(filter.IsAllowed(rmc));
        Assert.False(filter.IsAllowed(gsa));
    }

    [Fact]
    public void AllowList_MatchesFullType()
    {
        // Arrange
        var config = new SentenceFilterConfiguration
        {
            Mode = FilterMode.AllowList,
            AllowedTypes = new HashSet<string> { "GNGGA" }
        };
        var filter = new SentenceFilter(config);
        var gnGga = CreateSentence("GNGGA");

        // Act & Assert
        Assert.True(filter.IsAllowed(gnGga));
    }

    [Fact]
    public void BlockList_BlocksSpecifiedTypes()
    {
        // Arrange
        var config = new SentenceFilterConfiguration
        {
            Mode = FilterMode.BlockList,
            BlockedTypes = new HashSet<string> { "GSV" }
        };
        var filter = new SentenceFilter(config);
        var gga = CreateSentence("GNGGA");
        var gsv = CreateSentence("GPGSV");

        // Act & Assert
        Assert.True(filter.IsAllowed(gga));
        Assert.False(filter.IsAllowed(gsv));
    }

    [Fact]
    public void DefaultFilter_AllowsEverything()
    {
        // Arrange
        var filter = new SentenceFilter();
        var gga = CreateSentence("GNGGA");
        var rmc = CreateSentence("GNRMC");
        var gsa = CreateSentence("GNGSA");
        var gsv = CreateSentence("GPGSV");

        // Act & Assert
        Assert.True(filter.IsAllowed(gga));
        Assert.True(filter.IsAllowed(rmc));
        Assert.True(filter.IsAllowed(gsa));
        Assert.True(filter.IsAllowed(gsv));
    }
}
