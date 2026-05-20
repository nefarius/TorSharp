using System.IO;
using Microsoft.Extensions.Logging;
using Nefarius.Utilities.TorProxy.Tools;

namespace Nefarius.Utilities.TorProxy.Logging;

/// <summary>
/// Routes <see cref="DataEventArgs"/> lines emitted by the Tor and Privoxy sub-processes to
/// the Microsoft logging infrastructure. Each tool gets its own logger category so consumers
/// can filter them independently via configuration.
/// </summary>
internal sealed class ToolLogger
{
    private readonly ILogger _torLogger;
    private readonly ILogger _privoxyLogger;
    private readonly TorProxySettings _settings;

    /// <summary>Logger category name used for all output originating from the Tor process.</summary>
    public const string TorCategory = "Nefarius.Utilities.TorProxy.Tor";

    /// <summary>Logger category name used for all output originating from the Privoxy process.</summary>
    public const string PrivoxyCategory = "Nefarius.Utilities.TorProxy.Privoxy";

    public ToolLogger(ILoggerFactory loggerFactory, TorProxySettings settings)
    {
        _torLogger = loggerFactory.CreateLogger(TorCategory);
        _privoxyLogger = loggerFactory.CreateLogger(PrivoxyCategory);
        _settings = settings;
    }

    /// <summary>
    /// Handles a single output line from any managed tool process.
    /// </summary>
    public void HandleLine(DataEventArgs e, bool isStderr)
    {
        if (e.Data == null)
        {
            return;
        }

        var execName = Path.GetFileName(e.ExecutablePath);
        var isPrivoxy = execName.StartsWith("privoxy", System.StringComparison.OrdinalIgnoreCase);

        if (isPrivoxy)
        {
            HandlePrivoxyLine(e.Data, isStderr);
        }
        else
        {
            HandleTorLine(e.Data, isStderr);
        }
    }

    private void HandleTorLine(string line, bool isStderr)
    {
        var fallback = isStderr ? LogLevel.Warning : LogLevel.Information;
        var (level, message) = _settings.LogTimestampStripping
            ? TorLogLineParser.Parse(line, fallback)
            : (fallback, line);

        if (level < _settings.MinTorLogLevel || !_torLogger.IsEnabled(level))
        {
            return;
        }

        _torLogger.Log(level, "{Message}", message);
    }

    private void HandlePrivoxyLine(string line, bool isStderr)
    {
        var fallback = isStderr ? LogLevel.Warning : LogLevel.Information;
        var (level, message) = _settings.LogTimestampStripping
            ? PrivoxyLogLineParser.Parse(line, fallback)
            : (fallback, line);

        if (level < _settings.MinPrivoxyLogLevel || !_privoxyLogger.IsEnabled(level))
        {
            return;
        }

        _privoxyLogger.Log(level, "{Message}", message);
    }
}
