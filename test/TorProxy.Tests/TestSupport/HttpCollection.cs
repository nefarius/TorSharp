using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests.TestSupport
{
    [CollectionDefinition(Name)]
    public class HttpCollection : ICollectionFixture<HttpFixture>
    {
        public const string Name = nameof(HttpCollection);
    }
}
