using Nefarius.Utilities.TorProxy.Tests.TestSupport;
using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests
{
    public class TorProxyPrivoxySettingsTests
    {
        [Fact]
        [DisplayTestMethodName]
        public void HasExpectedDefaultSocksPort()
        {
            Assert.Equal(18118, new TorProxyPrivoxySettings().Port);
        }

        [Fact]
        [DisplayTestMethodName]
        public void IsDisabledByDefault()
        {
            Assert.True(new TorProxyPrivoxySettings().Disable);
        }
    }
}
