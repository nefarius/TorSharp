using System;

namespace Nefarius.Utilities.TorProxy.Adapters;

internal interface IRandom : IDisposable
{
    void GetBytes(byte[] bytes);
}
