using System;

namespace Nefarius.Utilities.TorProxy;

/// <summary>
/// Update information for the tools needed to run <see cref="TorProxy"/>.
/// </summary>
public class ToolUpdates
{
    public ToolUpdates(ToolUpdate? privoxy, ToolUpdate tor)
    {
        Privoxy = privoxy;
        Tor = tor ?? throw new ArgumentNullException(nameof(tor));
    }

    /// <summary>
    /// Whether or not there is an update available for one or more tools.
    /// </summary>
    public bool HasUpdate => (Privoxy != null && Privoxy.HasUpdate) || Tor.HasUpdate;

    /// <summary>
    /// Update information for Privoxy.
    /// </summary>
    public ToolUpdate? Privoxy { get; }

    /// <summary>
    /// Update information for Tor.
    /// </summary>
    public ToolUpdate Tor { get; }
}
