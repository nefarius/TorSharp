#if NETSTANDARD2_0
using System.Runtime.Serialization;
#endif

namespace Nefarius.Utilities.TorProxy.Tools.Tor;

[System.Serializable]
public class TorControlException : TorProxyException
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
