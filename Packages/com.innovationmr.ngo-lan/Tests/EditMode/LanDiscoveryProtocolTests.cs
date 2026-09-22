using System.Net;
using InnovationMR.NgoLan.Discovery;
using NUnit.Framework;

namespace InnovationMR.NgoLan.Tests
{
    public sealed class LanDiscoveryProtocolTests
    {
        [Test]
        public void EncodeDecode_RoundTrips_AndUsesSenderAddress()
        {
            var source = new LanSessionInfo("id-1", "测试房间", "1.0", "{\"mode\":\"mr\"}",
                string.Empty, 7777, 2, 8);
            var sender = new IPEndPoint(IPAddress.Parse("192.168.1.20"), 47777);

            bool ok = LanDiscoveryProtocol.TryDecode(LanDiscoveryProtocol.Encode(source), sender, out var decoded);

            Assert.That(ok, Is.True);
            Assert.That(decoded.Id, Is.EqualTo("id-1"));
            Assert.That(decoded.Name, Is.EqualTo("测试房间"));
            Assert.That(decoded.Address, Is.EqualTo("192.168.1.20"));
            Assert.That(decoded.Port, Is.EqualTo(7777));
        }

        [Test]
        public void Decode_RejectsForeignOrOversizedPackets()
        {
            var sender = new IPEndPoint(IPAddress.Loopback, 47777);
            Assert.That(LanDiscoveryProtocol.TryDecode(new byte[] { 1, 2, 3 }, sender, out _), Is.False);
            Assert.That(LanDiscoveryProtocol.TryDecode(new byte[LanDiscoveryProtocol.MaxPacketBytes + 1], sender, out _), Is.False);
        }
    }
}
