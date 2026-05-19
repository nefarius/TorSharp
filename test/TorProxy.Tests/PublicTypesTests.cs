using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace UnsharedNamespace
{
    public class PublicTypesTests
    {
        [Fact]
        public void TheSetOfPublicTypesIsExpected()
        {
            var expectedTypes = new List<Type>()
            {
                typeof(Nefarius.Utilities.TorProxy.ITorProxy),
                typeof(Nefarius.Utilities.TorProxy.ITorProxyToolFetcher),
                typeof(Nefarius.Utilities.TorProxy.ToolDownloadStrategy),
                typeof(Nefarius.Utilities.TorProxy.Tools.DataEventArgs),
                typeof(Nefarius.Utilities.TorProxy.Tools.DownloadableFile),
                typeof(Nefarius.Utilities.TorProxy.Tools.FileNamePatternAndFormat),
                typeof(Nefarius.Utilities.TorProxy.Tools.Tor.TorControlClient),
                typeof(Nefarius.Utilities.TorProxy.Tools.Tor.TorControlException),
                typeof(Nefarius.Utilities.TorProxy.Tools.ZippedToolFormat),
                typeof(Nefarius.Utilities.TorProxy.ToolUpdate),
                typeof(Nefarius.Utilities.TorProxy.ToolUpdates),
                typeof(Nefarius.Utilities.TorProxy.ToolUpdateStatus),
                typeof(Nefarius.Utilities.TorProxy.TorProxyArchitecture),
                typeof(Nefarius.Utilities.TorProxy.TorProxyException),
                typeof(Nefarius.Utilities.TorProxy.TorProxyOSPlatform),
                typeof(Nefarius.Utilities.TorProxy.TorProxyPrivoxySettings),
                typeof(Nefarius.Utilities.TorProxy.TorProxy),
                typeof(Nefarius.Utilities.TorProxy.TorProxyExtensions),
                typeof(Nefarius.Utilities.TorProxy.TorProxySettings),
                typeof(Nefarius.Utilities.TorProxy.TorProxyToolFetcher),
                typeof(Nefarius.Utilities.TorProxy.TorProxyTorSettings),
            };

            var publicTypes = typeof(Nefarius.Utilities.TorProxy.TorProxy)
                .Assembly
                .GetTypes()
                .Where(x => x.IsPublic)
                .ToList();

            Assert.Empty(publicTypes.Except(expectedTypes));
            Assert.Empty(expectedTypes.Except(publicTypes));
        }
    }
}
