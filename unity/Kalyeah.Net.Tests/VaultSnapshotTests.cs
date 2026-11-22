using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>A vault in flight reaches every client: its time left, path and cover height ride the snapshot (docs/SPEC.md §7).</summary>
    public class VaultSnapshotTests
    {
        [Test]
        public void AVaultInFlight_ReachesTheMirror_AndAnEndedOneClears()
        {
            var server = new MatchSim(NetKit.Rules.Duplicate(), NetKit.Layout, 3);
            PlayerState kid = server.AddPlayer(1, 0);
            kid.VaultLeft = 0.2;
            kid.VaultFrom = new Vec2(1.0, 2.0);
            kid.VaultTo = new Vec2(1.0, 4.0);
            kid.VaultHeight = 0.9;

            var mirror = new MatchSim(NetKit.Rules.Duplicate(), NetKit.Layout, 3);
            PlayerState copy = mirror.AddPlayer(1, 0);
            Snapshot.Apply(mirror, Snapshot.Capture(server, new OrderedDict<int, int>()), 2);
            Assert.AreEqual(0.2, copy.VaultLeft, 0.01);
            Assert.AreEqual(4.0, copy.VaultTo.y, 0.01);
            Assert.AreEqual(2.0, copy.VaultFrom.y, 0.01);
            Assert.AreEqual(0.9, copy.VaultHeight, 0.01);

            kid.VaultLeft = 0.0;
            Snapshot.Apply(mirror, Snapshot.Capture(server, new OrderedDict<int, int>()), 2);
            Assert.AreEqual(0.0, copy.VaultLeft);
            Assert.AreEqual(Vec2.Zero, copy.VaultTo);
        }
    }
}
