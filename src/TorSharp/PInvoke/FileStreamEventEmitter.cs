using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Knapcode.TorSharp.PInvoke;

internal class FileStreamEventEmitter : IDisposable
{
    private FileStream? _fileStream;
    private StreamReader? _streamReader;
    private readonly CancellationTokenSource _cts;
    private readonly Action<string?> _onData;

    public FileStreamEventEmitter(IntPtr handle, Action<string?> onData)
    {
        _cts = new CancellationTokenSource();
        _onData = onData;

        // Initialise synchronously so Dispose() can always reach and release the handles,
        // even if called before the background task has had a chance to run.
        _fileStream = new FileStream(new SafeFileHandle(handle, ownsHandle: true), FileAccess.Read);
        _streamReader = new StreamReader(_fileStream);

        var _ = Task.Run(ReadLoopAsync);
    }

    private async Task ReadLoopAsync()
    {
        // Guard: Dispose() may have been called between the constructor and this point.
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        string? line;
        do
        {
            try
            {
                line = await _streamReader!.ReadLineAsync().ConfigureAwait(false);
                _onData(line);
            }
            catch (Exception)
            {
                // Covers both normal EOF and ObjectDisposedException from a concurrent Dispose().
                break;
            }
        }
        while (!_cts.IsCancellationRequested && line != null);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _streamReader?.Dispose();
        _fileStream?.Dispose();
        _cts.Dispose();
    }
}
