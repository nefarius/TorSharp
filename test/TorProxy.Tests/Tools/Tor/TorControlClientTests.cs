using System.Threading.Tasks;
using Nefarius.Utilities.TorProxy.Tests.TestSupport;
using Nefarius.Utilities.TorProxy.Tools.Tor;
using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests.Tools.Tor
{
    public class TorControlClientTests
    {
        [Fact]
        [DisplayTestMethodName]
        public async Task CanDisposeAfterFailedConnect()
        {
            TorControlClient? client = null;
            try
            {
                client = new TorControlClient();
                await client.ConnectAsync("300.0.0.0", 12345);
            }
            catch
            {
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}
