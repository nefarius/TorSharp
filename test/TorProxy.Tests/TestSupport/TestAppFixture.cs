using System;
using System.IO;

namespace Nefarius.Utilities.TorProxy.Tests.TestSupport
{
    public class TestAppFixture : IDisposable
    {
        public TestAppFixture()
        {
            SharedDirectory = new TestDirectory(output: null);
            Directory.CreateDirectory(SharedDirectory);
        }

        public TestDirectory SharedDirectory { get; }

        public void Dispose()
        {
            SharedDirectory.Dispose();
        }
    }
}
