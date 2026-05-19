using System;
using System.Linq;
using Nefarius.Utilities.TorProxy.Adapters;
using Nefarius.Utilities.TorProxy.Tests.TestSupport;
using Nefarius.Utilities.TorProxy.Tools.Tor;
using NSubstitute;
using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests.Tools.Tor;

public class TorPasswordHasherTests
{
    [Fact]
    [DisplayTestMethodName]
    public void TorPasswordHasher_MatchesKnownInput()
    {
        var password = "foobar";
        var expected = "16:F5B2CC984516D0C360326BB33D50A4D75F2496D27ED572A7A5C6676648";

        MatchesExpectedHashedPassword(password, expected);
    }

    private void MatchesExpectedHashedPassword(string password, string expected)
    {
        var salt = GetSalt(expected);

        var random = Substitute.For<IRandom>();
        random.When(x => x.GetBytes(Arg.Any<byte[]>()))
              .Do(ci =>
              {
                  var buf = ci.Arg<byte[]>();
                  Buffer.BlockCopy(salt, 0, buf, 0, Math.Min(salt.Length, buf.Length));
              });

        var randomFactory = Substitute.For<IRandomFactory>();
        randomFactory.Create().Returns(random);

        var torPasswordHasher = new TorPasswordHasher(randomFactory);

        var actual = torPasswordHasher.HashPassword(password);

        Assert.Equal(expected, actual);
    }

    private byte[] GetSalt(string torPassword)
    {
        var salt = torPassword.Substring(3, 16);
        return HexToBytes(salt);
    }

    private byte[] HexToBytes(string hex) =>
        Enumerable
            .Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
}
