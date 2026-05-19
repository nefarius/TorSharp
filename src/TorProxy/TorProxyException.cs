using System;
#if NETSTANDARD2_0
using System.Runtime.Serialization;
#endif

namespace Nefarius.Utilities.TorProxy;

[Serializable]
public class TorProxyException : Exception
{
    public TorProxyException(string message) : base(message)
    {
    }

    public TorProxyException(string message, Exception innerException) : base(message, innerException)
    {
    }

#if NETSTANDARD2_0
    protected TorProxyException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
#endif
}
