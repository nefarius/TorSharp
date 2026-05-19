using System;
using System.Threading.Tasks;

namespace Nefarius.Utilities.TorProxy.Tools;

internal interface IToolRunner : IDisposable
{
    Task StartAsync(Tool tool);
    void Stop();
    event EventHandler<DataEventArgs> Stdout;
    event EventHandler<DataEventArgs> Stderr;
}
