using System.Reflection;
using Xunit.Sdk;

namespace Nefarius.Utilities.TorProxy.Tests.TestSupport
{
    /// <summary>
    /// Source: https://stackoverflow.com/a/26042654
    /// </summary>
    public class DisplayTestMethodNameAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            // Console.WriteLine($"🌟 Starting {methodUnderTest.Name}");
        }

        public override void After(MethodInfo methodUnderTest)
        {
            // Console.WriteLine($"🛑 Finished {methodUnderTest.Name}");
        }
    }
}