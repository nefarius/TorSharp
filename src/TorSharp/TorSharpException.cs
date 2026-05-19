using System;
#if NETSTANDARD2_0
using System.Runtime.Serialization;
#endif

namespace Knapcode.TorSharp;

[Serializable]
public class TorSharpException : Exception
{
    public TorSharpException(string message) : base(message)
    {
    }

    public TorSharpException(string message, Exception innerException) : base(message, innerException)
    {
    }

#if NETSTANDARD2_0
    protected TorSharpException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
#endif
}
