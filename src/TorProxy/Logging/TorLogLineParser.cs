using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Nefarius.Utilities.TorProxy.Logging;

/// <summary>
/// Parses a single stdout/stderr line produced by the Tor process into a
/// <see cref="Microsoft.Extensions.Logging.LogLevel"/> and a clean message body (with the
/// leading timestamp+level prefix stripped).
/// </summary>
internal static class TorLogLineParser
{
    // Example: "May 20 14:08:32.000 [notice] Heartbeat: ..."
    private static readonly Regex LinePattern = new(
        @"^\w{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?\s+\[(?<lvl>debug|info|notice|warn|err)\]\s+(?<msg>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses <paramref name="line"/> and returns the mapped <see cref="LogLevel"/> and the
    /// bare message text. When the line does not match the Tor prefix pattern, the entire line
    /// is returned as the message with <paramref name="fallbackLevel"/> as the level.
    /// </summary>
    public static (LogLevel Level, string Message) Parse(string line, LogLevel fallbackLevel = LogLevel.Information)
    {
        if (string.IsNullOrEmpty(line))
        {
            return (fallbackLevel, line ?? string.Empty);
        }

        var match = LinePattern.Match(line);
        if (!match.Success)
        {
            return (fallbackLevel, line);
        }

        var level = match.Groups["lvl"].Value.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info"  => LogLevel.Debug,     // Tor "info" is very chatty; map to Debug
            "notice" => LogLevel.Information,
            "warn"  => LogLevel.Warning,
            "err"   => LogLevel.Error,
            _       => fallbackLevel,
        };

        return (level, match.Groups["msg"].Value);
    }
}
