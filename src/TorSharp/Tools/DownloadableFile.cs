using System;

namespace Knapcode.TorSharp.Tools;

public class DownloadableFile
{
    public DownloadableFile(Version version, Uri url, ZippedToolFormat format)
        : this(version, url, format, sha256: null)
    {
    }

    public DownloadableFile(Version version, Uri url, ZippedToolFormat format, string? sha256)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("The URL must be absolute.", nameof(url));
        }

        Version = version ?? throw new ArgumentNullException(nameof(version));
        Url = url;
        Format = format;
        Sha256 = sha256;
    }

    /// <summary>
    /// The version of the tool to download.
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// The absolute URL of the tool download.
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// The format of the downloadable file.
    /// </summary>
    public ZippedToolFormat Format { get; }

    /// <summary>
    /// The expected lowercase hex-encoded SHA256 digest of the downloaded file, or <c>null</c>
    /// if no checksum is available (upstream-discovered files). When present, the fetcher
    /// verifies the digest after download and throws if it does not match.
    /// </summary>
    public string? Sha256 { get; }
}
