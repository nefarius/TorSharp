using Nefarius.Utilities.TorProxy.Tests.TestSupport;
using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests
{
    public class TorProxyTorSettingsTests
    {
        [Fact]
        [DisplayTestMethodName]
        public void HasExpectedDefaultSocksPort()
        {
            Assert.Equal(19050, new TorProxyTorSettings().SocksPort);
        }

        [Fact]
        [DisplayTestMethodName]
        public void HasExpectedDefaultControlPort()
        {
            Assert.Equal(19051, new TorProxyTorSettings().ControlPort);
        }

        [Fact]
        [DisplayTestMethodName]
        public void HasDefaultControlPassword()
        {
            var settings = new TorProxyTorSettings();

            Assert.NotNull(settings.ControlPassword);
            Assert.Null(settings.HashedControlPassword);
            Assert.Equal(TorProxyTorSettings.DefaultControlPassword, settings.ControlPassword);
        }
    }
}
