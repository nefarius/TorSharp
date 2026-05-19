using System.Security.Cryptography;

namespace Nefarius.Utilities.TorProxy.Adapters;

internal class RandomFactory : IRandomFactory
{
    public IRandom Create() => new Random(RandomNumberGenerator.Create());
}
