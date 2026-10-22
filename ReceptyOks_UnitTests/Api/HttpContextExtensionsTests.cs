using Microsoft.AspNetCore.Http;
using ReceptyOks.Api.Extensions;
using System.Net;

namespace ReceptyOks_UnitTests.Api;

[TestFixture]
public class HttpContextExtensionsTests
{
    private static DefaultHttpContext CreateContext(IPAddress? remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIp;
        return context;
    }

    [Test]
    public void GetClientIp_NullRemoteIp_ReturnsUnknown()
    {
        var context = CreateContext(null);

        Assert.That(context.GetClientIp(), Is.EqualTo("unknown"));
    }

    [TestCase("203.0.113.42")]
    [TestCase("127.0.0.1")]
    [TestCase("10.0.0.1")]
    public void GetClientIp_IPv4_ReturnsAddress(string address)
    {
        var context = CreateContext(IPAddress.Parse(address));

        Assert.That(context.GetClientIp(), Is.EqualTo(address));
    }

    [Test]
    public void GetClientIp_IPv4MappedToIPv6_ReturnsPlainIPv4()
    {
        var context = CreateContext(IPAddress.Parse("::ffff:203.0.113.42"));

        Assert.That(context.GetClientIp(), Is.EqualTo("203.0.113.42"));
    }

    [Test]
    public void GetClientIp_IPv4AndMappedIPv4_ShareSamePartition()
    {
        var plain = CreateContext(IPAddress.Parse("203.0.113.42")).GetClientIp();
        var mapped = CreateContext(IPAddress.Parse("::ffff:203.0.113.42")).GetClientIp();

        Assert.That(mapped, Is.EqualTo(plain));
    }

    [Test]
    public void GetClientIp_IPv6_ReturnsSlash64Prefix()
    {
        var context = CreateContext(IPAddress.Parse("2001:db8:abcd:12:1a2b:3c4d:5e6f:7788"));

        Assert.That(context.GetClientIp(), Is.EqualTo("2001:db8:abcd:12::/64"));
    }

    [Test]
    public void GetClientIp_IPv6AddressesInSameSlash64_ShareSamePartition()
    {
        var first = CreateContext(IPAddress.Parse("2001:db8:abcd:12::1")).GetClientIp();
        var second = CreateContext(IPAddress.Parse("2001:db8:abcd:12:ffff:ffff:ffff:ffff")).GetClientIp();

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void GetClientIp_IPv6AddressesInDifferentSlash64_UseDifferentPartitions()
    {
        var first = CreateContext(IPAddress.Parse("2001:db8:abcd:12::1")).GetClientIp();
        var second = CreateContext(IPAddress.Parse("2001:db8:abcd:13::1")).GetClientIp();

        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void GetClientIp_IPv6Loopback_ReturnsZeroPrefix()
    {
        var context = CreateContext(IPAddress.IPv6Loopback);

        Assert.That(context.GetClientIp(), Is.EqualTo("::/64"));
    }

    [Test]
    public void GetClientIp_IPv6WithScopeId_IgnoresScope()
    {
        var context = CreateContext(IPAddress.Parse("fe80::1234:5678%3"));

        Assert.That(context.GetClientIp(), Is.EqualTo("fe80::/64"));
    }
}
