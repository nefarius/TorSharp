using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit.Abstractions;

namespace Nefarius.Utilities.TorProxy.Tests.TestSupport
{
    public static class Extensions
    {
        public static void WriteLine(this ITestOutputHelper output, TorProxySettings settings)
        {
            var serializerSettings = new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter()
                }
            };

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented, serializerSettings);

            output.WriteLine($"{nameof(TorProxySettings)}:" + Environment.NewLine + json);
        }
    }
}