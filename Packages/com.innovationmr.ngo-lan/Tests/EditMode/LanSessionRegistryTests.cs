using InnovationMR.NgoLan.Discovery;
using NUnit.Framework;

namespace InnovationMR.NgoLan.Tests
{
    public sealed class LanSessionRegistryTests
    {
        private static LanSessionInfo Session(string id = "server-1", string address = "10.0.0.2", ushort port = 7777) =>
            new(id, "Room", "1.0", string.Empty, address, port, 1, 4);

        [Test]
        public void RemoveExpired_RemovesAtTimeoutBoundary()
        {
            var registry = new LanSessionRegistry(8);
            registry.Upsert(Session(), 100);

            Assert.That(registry.RemoveExpired(103.999, 4), Is.False);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.RemoveExpired(104, 4), Is.True);
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        public void Rediscovery_RefreshesLastSeen()
        {
            var registry = new LanSessionRegistry(8);
            registry.Upsert(Session(), 100);
            registry.Upsert(Session(), 103);

            registry.RemoveExpired(104.5, 4);

            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Capacity_RejectsAdditionalSessions()
        {
            var registry = new LanSessionRegistry(1);
            Assert.That(registry.Upsert(Session("a"), 1), Is.True);
            Assert.That(registry.Upsert(Session("b", "10.0.0.3"), 1), Is.False);
            Assert.That(registry.Count, Is.EqualTo(1));
        }
    }
}
