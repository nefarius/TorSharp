using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Nefarius.Utilities.TorProxy.Logging;

/// <summary>
/// Best-effort parser for Privoxy log lines.
/// Privoxy emits lines in the shape:
/// <c>YYYY-MM-DD HH:MM:SS.fff +ZZZZ [PID] LEVEL: message</c>
/// or the older compact form without the timezone component.
/// </summary>
internal static class PrivoxyLogLineParser
{
    // Example: "2024-05-20 14:08:32.123 +0200 [12345] Info: ..."
    private static readonly Regex LinePattern = new(
        @"^\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:\s+[+-]\d{4})?\s+\[\d+\]\s+(?<lvl>\w+):\s+(?<msg>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses <paramref name="line"/> and returns the mapped <see cref="LogLevel"/> and the
    /// bare message text. When the line does not match the Privoxy prefix pattern, the entire
    /// line is returned as the message with <paramref name="fallbackLevel"/> as the level.
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
            "fatal" => LogLevel.Critical,
            "error" => LogLevel.Error,
            "warning" => LogLevel.Warning,
            "warn" => LogLevel.Warning,
            "info" => LogLevel.Information,
            "debug" => LogLevel.Debug,
            _ => fallbackLevel,
        };

        return (level, match.Groups["msg"].Value);
    }
}
