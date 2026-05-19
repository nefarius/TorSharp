using System.Security.Cryptography;

namespace Knapcode.TorSharp.Adapters;

internal class RandomFactory : IRandomFactory
{
    public IRandom Create() => new Random(RandomNumberGenerator.Create());
}
