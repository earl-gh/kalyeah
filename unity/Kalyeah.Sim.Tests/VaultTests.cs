using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// The context jump's vault (docs/SPEC.md §7): one jump button jumps, or vaults a low cover the kid is moving into, in 0.4 s,
    /// landing on the far side. Low covers stop walking but not throws, and the Barangay map has mirrored low covers to vault.
    /// </summary>
    public class VaultTests
    {
        const double CoverZ = 20.0;

        /// <summary>An open street with one low wall across the path, at <paramref name="height"/> metres.</summary>
        static MatchSim WallSim(double height, out PlayerState kid, bool blockFarSide = false)
        {
            MapLayout layout = OpenLayout();
            layout.LowCovers = new List<Rect2> { new Rect2(-2.0, CoverZ, 4.0, 0.6) };
            layout.LowCoverHeights = new List<float> { (float)height };
            if (blockFarSide) layout.Obstacles = new List<Rect2> { new Rect2(-2.0, CoverZ + 0.6, 4.0, 3.0) };
            GameRules rules = Rules();
            rules.TripChance = 0.0;
            var sim = new MatchSim(rules, layout, 5);
            kid = sim.AddPlayer(1, 0);
            kid.Position = new Vec2(0.0, CoverZ - kid.Radius - 0.3);
            return sim;
        }

        [Test]
        public void JumpWhileMovingIntoALowWall_VaultsItIn04Seconds_AndLandsOnTheFarSide()
        {
            MatchSim sim = WallSim(0.9, out PlayerState kid);
            GameRules rules = sim.Rules;
            Assert.AreEqual(0.4, rules.VaultDuration, 1e-9, "docs/SPEC.md §7: a vault takes 0.4 s");

            Tick(sim, 1, new Vec2(0.0, 1.0), PlayerInput.BtnJump);
            Assert.Greater(kid.VaultLeft, 0.0, "the jump button vaults a low wall the kid is moving into");
            Assert.AreEqual(0.0, kid.VelY, 1e-9, "a vault is not a jump");

            double peak = 0.0;
            int ticks = 1;
            while (kid.VaultLeft > 0.0 && ticks < 60)
            {
                Tick(sim, 1, new Vec2(0.0, 1.0), PlayerInput.BtnJump);
                peak = System.Math.Max(peak, kid.AirY);
                ticks++;
            }
            Assert.AreEqual(rules.VaultDuration, ticks * DT, DT + 1e-9, "the vault lasts 0.4 s");
            Assert.Greater(peak, 0.9, "the arc clears the wall");
            Assert.AreEqual(0.0, kid.AirY, 1e-9, "the kid lands");
            Assert.Greater(kid.Position.y, CoverZ + 0.6, "the kid lands past the wall");
        }

        [Test]
        public void JumpStandingStill_OrWithNoCoverAhead_IsAPlainJump()
        {
            MatchSim sim = WallSim(0.9, out PlayerState kid);
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnJump);
            Assert.AreEqual(0.0, kid.VaultLeft, "standing still: a jump");
            Assert.Greater(kid.VelY, 0.0);

            MatchSim away = WallSim(0.9, out PlayerState walker);
            Tick(away, 1, new Vec2(0.0, -1.0), PlayerInput.BtnJump);
            Assert.AreEqual(0.0, walker.VaultLeft, "moving away from the wall: a jump");
            Assert.Greater(walker.VelY, 0.0);
        }

        [Test]
        public void ACoverTooHighToVault_OrWithNoRoomBehind_StaysAJumpOnTheNearSide()
        {
            MatchSim high = WallSim(1.6, out PlayerState kid);
            Assert.Greater(1.6, high.Rules.VaultMaxHeight);
            Tick(high, 1, new Vec2(0.0, 1.0), PlayerInput.BtnJump);
            Assert.AreEqual(0.0, kid.VaultLeft, "higher than vault_max_height: no vault");
            Run(high, 1, new Vec2(0.0, 1.0), 30);
            Assert.Less(kid.Position.y, CoverZ, "the wall still stops the kid");

            MatchSim blocked = WallSim(0.9, out PlayerState boxed, blockFarSide: true);
            Tick(blocked, 1, new Vec2(0.0, 1.0), PlayerInput.BtnJump);
            Assert.AreEqual(0.0, boxed.VaultLeft, "no room on the far side: no vault");
        }

        [Test]
        public void LowCoversStopWalking_ButNotThrows()
        {
            MatchSim sim = WallSim(0.9, out PlayerState kid);
            Run(sim, 1, new Vec2(0.0, 1.0), 45);
            Assert.Less(kid.Position.y, CoverZ, "walking into a low wall stops at it");
            Rect2 wall = sim.LowCovers[0];
            foreach (Rect2 rect in sim.BlockingRectsForTeam(0))
                Assert.IsFalse(rect.Position == wall.Position && rect.Size == wall.Size, "a throw flies over a low wall");
        }

        [Test]
        public void BeingTaggedMidVault_EndsTheVault()
        {
            MatchSim sim = WallSim(0.9, out PlayerState kid);
            Tick(sim, 1, new Vec2(0.0, 1.0), PlayerInput.BtnJump);
            Assert.Greater(kid.VaultLeft, 0.0);
            sim.Objectives.Tag(sim, kid);
            Assert.AreEqual(0.0, kid.VaultLeft);
            Assert.AreEqual(0.0, kid.AirY);
        }

        [Test]
        public void TheBarangayHasMirroredLowCoversToVault_ClearOfBuildingsAndBases()
        {
            MapLayout layout = Layout;
            Assert.Greater(layout.LowCovers.Count, 0, "the Barangay has low covers to vault");
            Assert.AreEqual(layout.LowCovers.Count, layout.LowCoverHeights.Count);
            List<Rect2> covers = layout.LowCoverRects();
            List<Rect2> buildings = layout.ObstacleRects();
            int half = layout.LowCovers.Count;
            for (int i = 0; i < covers.Count; i++)
            {
                double height = layout.LowCoverHeight(i);
                Assert.That(height, Is.InRange(0.5, Rules().VaultMaxHeight), $"low cover {i} is vaultable");
                Assert.IsTrue(layout.InArena(covers[i].GetCenter(), 1.0), $"low cover {i} is in the arena");
                Assert.IsFalse(layout.InBase(MapLayout.SideOwn, covers[i].GetCenter()), $"low cover {i} is outside the own base");
                Assert.IsFalse(layout.InBase(MapLayout.SideEnemy, covers[i].GetCenter()), $"low cover {i} is outside the enemy base");
                foreach (Rect2 building in buildings)
                    Assert.IsFalse(building.Grow(1.0).Intersects(covers[i]), $"low cover {i} keeps clear of buildings");
                if (i < half)
                {
                    Rect2 mirror = covers[i + half];
                    Assert.AreEqual(-covers[i].End.x, mirror.Position.x, 1e-6);
                    Assert.AreEqual(-covers[i].End.y, mirror.Position.y, 1e-6);
                    Assert.AreEqual(height, layout.LowCoverHeight(i + half), 1e-6, "a mirror has its twin's height");
                }
            }
        }
    }
}
