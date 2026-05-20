using Microsoft.Extensions.Logging;
using Nefarius.Utilities.TorProxy.Logging;
using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests.Tools;

public class TorLogLineParserTests
{
    [Theory]
    [InlineData("May 20 14:08:32.000 [notice] Heartbeat: uptime is 1 day", LogLevel.Information, "Heartbeat: uptime is 1 day")]
    [InlineData("May  5 09:00:00.000 [notice] Bootstrap complete", LogLevel.Information, "Bootstrap complete")]
    [InlineData("Jan  1 00:00:00.000 [debug] Some debug message", LogLevel.Debug, "Some debug message")]
    [InlineData("Jan  1 00:00:00.000 [info] Some info message", LogLevel.Debug, "Some info message")]
    [InlineData("Jan  1 00:00:00.000 [warn] A warning occurred", LogLevel.Warning, "A warning occurred")]
    [InlineData("Jan  1 00:00:00.000 [err] A fatal error occurred", LogLevel.Error, "A fatal error occurred")]
    public void Parse_KnownPrefix_ExtractsMappedLevelAndMessage(
        string line, LogLevel expectedLevel, string expectedMessage)
    {
        var (level, message) = TorLogLineParser.Parse(line);

        Assert.Equal(expectedLevel, level);
        Assert.Equal(expectedMessage, message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Some random line without a prefix")]
    [InlineData("2024-01-01 00:00:00 INFO not a tor format")]
    [InlineData("[notice] missing the date prefix")]
    public void Parse_UnrecognisedLine_ReturnsFallbackLevelAndRawLine(string line)
    {
        var fallback = LogLevel.Warning;
        var (level, message) = TorLogLineParser.Parse(line, fallback);

        Assert.Equal(fallback, level);
        Assert.Equal(line, message);
    }

    [Fact]
    public void Parse_NullLine_ReturnsFallbackAndEmptyString()
    {
        var (level, message) = TorLogLineParser.Parse(null!, LogLevel.Critical);

        Assert.Equal(LogLevel.Critical, level);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void Parse_LineWithoutMilliseconds_IsStillParsed()
    {
        const string line = "May 20 14:08:32 [notice] Bootstrapped 100%";
        var (level, message) = TorLogLineParser.Parse(line);

        Assert.Equal(LogLevel.Information, level);
        Assert.Equal("Bootstrapped 100%", message);
    }

    [Fact]
    public void Parse_DefaultFallback_IsInformation()
    {
        const string unrecognised = "not a tor line";
        var (level, _) = TorLogLineParser.Parse(unrecognised);

        Assert.Equal(LogLevel.Information, level);
    }
}
