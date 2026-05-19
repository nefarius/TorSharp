using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers.Tar;

namespace Knapcode.TorSharp.Tools;

internal class ArchiveUtility
{
    public static Task TestAsync(ZippedToolFormat format, string path) =>
        format switch
        {
            ZippedToolFormat.Zip => TestZipAsync(path),
            ZippedToolFormat.Deb => TestDebAsync(path),
            ZippedToolFormat.TarXz => TestTarXzAsync(path),
            ZippedToolFormat.TarGz => TestTarGzAsync(path),
            _ => throw new NotImplementedException($"Testing the zipped tool format {format} is not supported."),
        };

    public static Task ExtractAsync(ZippedToolFormat format, string path, string outputDir, Func<string, string?> getEntryPath) =>
        format switch
        {
            ZippedToolFormat.Zip => ExtractZipAsync(path, outputDir, getEntryPath),
            ZippedToolFormat.Deb => ExtractDebAsync(path, outputDir, getEntryPath),
            ZippedToolFormat.TarXz => ExtractTarXzAsync(path, outputDir, getEntryPath),
            ZippedToolFormat.TarGz => ExtractTarGzAsync(path, outputDir, getEntryPath),
            _ => throw new NotImplementedException($"Extracting the zipped tool format {format} is not supported."),
        };

    public static string GetFileExtension(ZippedToolFormat format) =>
        format switch
        {
            ZippedToolFormat.Zip => ".zip",
            ZippedToolFormat.Deb => ".deb",
            ZippedToolFormat.TarXz => ".tar.xz",
            ZippedToolFormat.TarGz => ".tar.gz",
            _ => throw new NotImplementedException($"The zipped tool format {format} does not have a known extension."),
        };

    public static Task TestZipAsync(string zipPath) =>
        ReadZipAsync(zipPath, outputDir: null, getEntryPath: null, shouldExtract: false);

    public static Task ExtractZipAsync(string zipPath, string outputDir, Func<string, string?> getEntryPath) =>
        ReadZipAsync(zipPath, outputDir, getEntryPath, shouldExtract: true);

    private static async Task ReadZipAsync(string zipPath, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
        using var fileStream = OpenForRead(zipPath);
        using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        if (!shouldExtract)
        {
            return;
        }

        var createdDirs = new HashSet<string>();
        foreach (var entry in zipArchive.Entries)
        {
            if (entry.FullName.EndsWith("/"))
            {
                continue;
            }

            var entryPath = getEntryPath!(entry.FullName);
            if (entryPath == null)
            {
                continue;
            }

            var fullEntryPath = Path.GetFullPath(Path.Combine(outputDir!, entryPath));
            var entryDir = Path.GetDirectoryName(fullEntryPath)!;
            if (createdDirs.Add(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            using var entryStream = entry.Open();
            using var outputStream = new FileStream(fullEntryPath, FileMode.Create);
            await entryStream.CopyToAsync(outputStream).ConfigureAwait(false);
        }
    }

    public static Task TestDebAsync(string debPath) =>
        ReadDebAsync(debPath, outputDir: null, getEntryPath: null, shouldExtract: false);

    public static Task ExtractDebAsync(string debPath, string outputDir, Func<string, string?> getEntryPath) =>
        ReadDebAsync(debPath, outputDir, getEntryPath, shouldExtract: true);

    private static async Task ReadDebAsync(string debPath, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
        // Debian packages (.deb) are "ar" archives with three files.
        //
        // 8 bytes for signature (should be "!<arch>\n")
        //
        // Each section has the following header:
        //  - 16 bytes for file identifier (ASCII)
        //  - 12 bytes for file modification timestamp (decimal)
        //  - 6 bytes for owner ID (decimal)
        //  - 6 bytes for group ID (decimal)
        //  - 8 bytes for file mode (octal)
        //  - 10 bytes for file size (decimal)
        //  - 2 bytes for ending characters ("`\n")
        //
        // The three sections are: Package Section, Control Section, Data Section.
        // We only care about the data section and we expect it to be a .tar.xz.
        const int signatureLength = 8;
        const int headerLength = 16 + 12 + 6 + 6 + 8 + 10 + 2;
        var buffer = new byte[headerLength];
        using var fileStream = OpenForRead(debPath);

        var read = fileStream.Read(buffer, 0, signatureLength);
        if (read != signatureLength || Encoding.ASCII.GetString(buffer, 0, read) != "!<arch>\n")
        {
            throw new TorSharpException("The Debian package did not have the expected file signature.");
        }

        read = fileStream.Read(buffer, 0, headerLength);
        var packageSectionHeader = ArFileHeader.Read(buffer);
        if (read != headerLength || packageSectionHeader.FileSize != 4)
        {
            throw new TorSharpException("The Debian package did not have the expected package section file header.");
        }
        fileStream.Position += packageSectionHeader.FileSize;

        ReadExact(fileStream, buffer, 0, headerLength);
        var controlSectionHeader = ArFileHeader.Read(buffer);
        fileStream.Position += controlSectionHeader.FileSize;

        ReadExact(fileStream, buffer, 0, headerLength);
        var dataSectionHeader = ArFileHeader.Read(buffer);
        var trimmedFileIdentifier = dataSectionHeader.FileIdentifier.TrimEnd();
        if (!trimmedFileIdentifier.EndsWith(".tar.xz"))
        {
            throw new TorSharpException("The Debian package's data section is expected to be a .tar.xz file.");
        }

        if (fileStream.Position + dataSectionHeader.FileSize != fileStream.Length)
        {
            throw new TorSharpException("The Debian package's data section is expected to reach the end of the .dev file.");
        }

        await ReadTarXzAsync(fileStream, outputDir, getEntryPath, shouldExtract).ConfigureAwait(false);
    }

    private class ArFileHeader
    {
        public ArFileHeader(string fileIdentifier, uint fileSize)
        {
            FileIdentifier = fileIdentifier;
            FileSize = fileSize;
        }

        public string FileIdentifier { get; }
        public uint FileSize { get; }

        public static ArFileHeader Read(byte[] header)
        {
            var fileIdentifierString = Encoding.ASCII.GetString(header, 0, 16);
            var fileSizeString = Encoding.ASCII.GetString(header, 16 + 12 + 6 + 6 + 8, 10);
            if (!uint.TryParse(fileSizeString, out var fileSize))
            {
                throw new TorSharpException("Could not read file size from the AR archive file header.");
            }

            return new ArFileHeader(fileIdentifierString, fileSize);
        }
    }

    public static Task TestTarXzAsync(string tarXzPath) =>
        ReadTarXzAsync(tarXzPath, outputDir: null, getEntryPath: null, shouldExtract: false);

    public static Task ExtractTarXzAsync(string tarXzPath, string outputDir, Func<string, string?> getEntryPath) =>
        ReadTarXzAsync(tarXzPath, outputDir, getEntryPath, shouldExtract: true);

    public static Task TestTarGzAsync(string tarGzPath) =>
        ReadTarGzAsync(tarGzPath, outputDir: null, getEntryPath: null, shouldExtract: false);

    public static Task ExtractTarGzAsync(string tarGzPath, string outputDir, Func<string, string?> getEntryPath) =>
        ReadTarGzAsync(tarGzPath, outputDir, getEntryPath, shouldExtract: true);

    private static async Task ReadTarXzAsync(string tarXzPath, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
        using var fileStream = OpenForRead(tarXzPath);
        await ReadTarXzAsync(fileStream, outputDir, getEntryPath, shouldExtract).ConfigureAwait(false);
    }

    private static async Task ReadTarXzAsync(FileStream fileStream, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
        using var xzStream = new XZStream(fileStream);
        await ReadTarAsync(xzStream, outputDir, getEntryPath, shouldExtract).ConfigureAwait(false);
    }

    private static async Task ReadTarGzAsync(string tarGzPath, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
        using var fileStream = OpenForRead(tarGzPath);
        await ReadTarGzAsync(fileStream, outputDir, getEntryPath, shouldExtract).ConfigureAwait(false);
    }

    private static async Task ReadTarGzAsync(FileStream fileStream, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await ReadTarAsync(gzipStream, outputDir, getEntryPath, shouldExtract).ConfigureAwait(false);
    }

    private static async Task ReadTarAsync(Stream tarStream, string? outputDir, Func<string, string?>? getEntryPath, bool shouldExtract)
    {
#pragma warning disable CAC001 // IAsyncDisposable.DisposeAsync() ConfigureAwait not supported on all targets
        await using var tarReader = await TarReader.OpenAsyncReader(tarStream).ConfigureAwait(false);
#pragma warning restore CAC001
        var createdDirs = new HashSet<string>();

        while (await tarReader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            if (!shouldExtract)
            {
                return;
            }

            if (tarReader.Entry.IsDirectory)
            {
                continue;
            }

            var entryPath = getEntryPath!(tarReader.Entry.Key!);
            if (entryPath == null)
            {
                continue;
            }

            var fullEntryPath = Path.GetFullPath(Path.Combine(outputDir!, entryPath));
            var entryDir = Path.GetDirectoryName(fullEntryPath)!;
            if (createdDirs.Add(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            using var entryStream = await tarReader.OpenEntryStreamAsync().ConfigureAwait(false);
            using var outputStream = new FileStream(fullEntryPath, FileMode.Create);
            await entryStream.CopyToAsync(outputStream).ConfigureAwait(false);
        }
    }

    private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
    {
#if NET7_0_OR_GREATER
        stream.ReadExactly(buffer, offset, count);
#else
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0) throw new TorSharpException("Unexpected end of stream while reading.");
            totalRead += read;
        }
#endif
    }

    private static FileStream OpenForRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
}
