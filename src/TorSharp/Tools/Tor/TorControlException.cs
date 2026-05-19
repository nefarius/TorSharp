#if NETSTANDARD2_0
using System.Runtime.Serialization;
#endif

namespace Knapcode.TorSharp.Tools.Tor;

[System.Serializable]
public class TorControlException : TorSharpException
{
    public TorControlException(string message) : base(message)
    {
    }

#if NETSTANDARD2_0
    protected TorControlException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
#endif
}
