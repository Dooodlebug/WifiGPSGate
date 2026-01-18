using System.Text;
using WifiGPSGate.Core.Models;
using WifiGPSGate.Core.Processing;

namespace WifiGPSGate.Tests;

public class RateLimiterTests
{
    private readonly NmeaParser _parser = new();

    private NmeaSentence CreateSentence(string type = "GNGGA")
    {
        var sentence = type switch
        {
            "GNGGA" => "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,47.0,M,,*47\r\n",
            "GNRMC" => "$GNRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A\r\n",
            _ => throw new ArgumentException($"Unknown type: {type}")
        };
        return _parser.TryParse(Encoding.ASCII.GetBytes(sentence))!;
    }

    [Fact]
    public void RateLimiter_AllowsFirstSentence()
    {
        // Arrange
        var config = new RateLimiterConfiguration { MaxRateHz = 1.0 };
        var limiter = new RateLimiter(config);
        var sentence = CreateSentence();

        // Act & Assert
        Assert.True(limiter.ShouldEmit(sentence));
    }

    [Fact]
    public void RateLimiter_BlocksRapidSentences()
    {
        // Arrange
        var config = new RateLimiterConfiguration { MaxRateHz = 1.0 };
        var limiter = new RateLimiter(config);
        var sentence = CreateSentence();

        // Act
        var first = limiter.ShouldEmit(sentence);
        var second = limiter.ShouldEmit(sentence);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task RateLimiter_AllowsAfterDelay()
    {
        // Arrange
        var config = new RateLimiterConfiguration { MaxRateHz = 10.0 }; // 100ms interval
        var limiter = new RateLimiter(config);
        var sentence = CreateSentence();

        // Act
        var first = limiter.ShouldEmit(sentence);
        await Task.Delay(150);
        var second = limiter.ShouldEmit(sentence);

        // Assert
        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public void RateLimiter_PerSentenceType_TracksIndependently()
    {
        // Arrange
        var config = new RateLimiterConfiguration { MaxRateHz = 1.0, PerSentenceType = true };
        var limiter = new RateLimiter(config);
        var gga = CreateSentence("GNGGA");
        var rmc = CreateSentence("GNRMC");

        // Act
        var ggaFirst = limiter.ShouldEmit(gga);
        var rmcFirst = limiter.ShouldEmit(rmc);
        var ggaSecond = limiter.ShouldEmit(gga);

        // Assert
        Assert.True(ggaFirst);
        Assert.True(rmcFirst);
        Assert.False(ggaSecond);
    }

    [Fact]
    public void RateLimiter_Reset_ClearsState()
    {
        // Arrange
        var config = new RateLimiterConfiguration { MaxRateHz = 1.0 };
        var limiter = new RateLimiter(config);
        var sentence = CreateSentence();

        // Act
        limiter.ShouldEmit(sentence);
        limiter.Reset();
        var afterReset = limiter.ShouldEmit(sentence);

        // Assert
        Assert.True(afterReset);
    }

    [Fact]
    public void RateLimiter_ZeroRate_AllowsAll()
    {
        // Arrange
        var config = new RateLimiterConfiguration { MaxRateHz = 0 };
        var limiter = new RateLimiter(config);
        var sentence = CreateSentence();

        // Act & Assert
        Assert.True(limiter.ShouldEmit(sentence));
        Assert.True(limiter.ShouldEmit(sentence));
        Assert.True(limiter.ShouldEmit(sentence));
    }
}
