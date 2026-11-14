using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Bots.BotKit;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>Port of tests/unit/test_fight_brain.gd (the sim parts): the sparring partner that fights back with the same PlayerInput as anyone.</summary>
    public class FightBrainTests
    {
        const int Player = 1;
        const int Bot = 2;
        static GameRules Rules => Kit.Data.GameRules;

        /// <summary>You at the middle of the street and a fighter `distance` metres up it, armed with `first` and `second`.</summary>
        static MatchSim Duel(double distance, string first = "bato_light", string second = "gunting_light", long seed = 7)
        {
            var sim = new MatchSim(Rules, OpenLayout(), seed);
            PlayerState me = sim.AddPlayer(Player, 0);
            me.Position = Vec2.Zero;
            var spot = new Vec2(0.0, -distance);
            sim.AddDummy(Bot, 1, spot, FightBrain.Fighter(spot));
            sim.SetLoadout(Bot, first, second);
            sim.SetLoadout(Player, "bola", "papel_shield");
            return sim;
        }

        static List<string> Casts(MatchSim sim)
        {
            var seen = new List<string>();
            sim.WeaponCast += (id, weapon) => { if (id == Bot) seen.Add(weapon); };
            return seen;
        }

        [Test]
        public void AFighterInRangeHurtsYou()
        {
            MatchSim sim = Duel(5.0);
            List<string> casts = Casts(sim);
            Kit.Wait(sim, 4.0);
            Assert.Greater(casts.Count, 0, "it cast");
            Assert.Less(sim.Players[Player].Hp, Rules.PlayerMaxHp, "and it landed");
        }

        [Test]
        public void AFighterDoesNotReactToAnEnemyOutOfItsAggroRange()
        {
            MatchSim sim = Duel(Rules.BotAggroRange + 4.0);
            List<string> casts = Casts(sim);
            Vec2 start = sim.Players[Bot].Position;
            Kit.Wait(sim, 4.0);
            Assert.AreEqual(0, casts.Count);
            Assert.AreEqual(start, sim.Players[Bot].Position, "it waits at its spot");
        }

        [Test]
        public void AFighterComesBackToItsSpotWhenYouLeave()
        {
            MatchSim sim = Duel(5.0);
            Kit.Wait(sim, 2.0);
            sim.Players[Player].Position = new Vec2(0.0, 45.0);
            Kit.Wait(sim, 8.0);
            Assert.Less(sim.Players[Bot].Position.DistanceTo(new Vec2(0.0, -5.0)), 0.5);
        }

        [Test]
        public void ItCastsNoFasterThanItsGap()
        {
            MatchSim sim = Duel(5.0, "bato_light", "tsinelas_light");
            sim.Players[Player].Hp = 9999;
            string primary = Rules.PrimaryWeapon?.Id ?? "";
            string ult = Rules.UltimateWeapon?.Id ?? "";
            var times = new List<double>();
            sim.WeaponCast += (id, weapon) => { if (id == Bot && weapon != primary && weapon != ult) times.Add(sim.MatchTime); };
            Kit.Wait(sim, 10.0);
            Assert.GreaterOrEqual(times.Count, 2);
            for (int i = 1; i < times.Count; i++) Assert.GreaterOrEqual(times[i] - times[i - 1], Rules.BotCastGap - 0.1);
        }

        [Test]
        public void ItDoesNotCastAWeaponThatCannotReach()
        {
            MatchSim sim = Duel(18.0, "gunting_light", "heal");
            List<string> casts = Casts(sim);
            Kit.Wait(sim, 0.5);
            CollectionAssert.DoesNotContain(casts, "gunting_light", "not yet in reach");
        }

        [Test]
        public void AHealthyFighterKeepsItsHealAndAHurtOneUsesIt()
        {
            MatchSim sim = Duel(5.0, "heal", "gunting_light");
            List<string> casts = Casts(sim);
            Kit.Wait(sim, 1.0);
            CollectionAssert.DoesNotContain(casts, "heal", "full health: no heal");
            sim.Players[Bot].Hp = 20;
            Kit.Wait(sim, 2.0);
            CollectionAssert.Contains(casts, "heal", "hurt: heals itself");
            Assert.Greater(sim.Players[Bot].Hp, 20);
        }

        [Test]
        public void AFighterClosesTheDistanceThenStandsOff()
        {
            MatchSim sim = Duel(22.0, "bato_light", "tsinelas_light");
            sim.Players[Player].Hp = 9999;
            Kit.Wait(sim, 6.0);
            double distance = sim.Players[Bot].Position.DistanceTo(sim.Players[Player].Position);
            Assert.Less(distance, 22.0, "it closed in");
            Assert.Greater(distance, FightBrain.StandoffMin - 1.0, "but it keeps a standoff");
        }

        [Test]
        public void ItNeverCastsWhileStunnedOrTagged()
        {
            MatchSim sim = Duel(5.0);
            List<string> casts = Casts(sim);
            sim.Players[Bot].Effects.Apply(StatusEffects.Type.Stun, 3.0);
            Kit.Wait(sim, 2.5);
            Assert.AreEqual(0, casts.Count, "stunned");
            sim.Damage(Bot, 9999);
            Kit.Wait(sim, 1.0);
            Assert.AreEqual(0, casts.Count, "tagged");
        }

        [Test]
        public void AFighterFetchesATaggedTeammateWhenNoEnemyIsClose()
        {
            MapLayout open = OpenLayout();
            var sim = new MatchSim(Rules, open, 3);
            Vec2 spot = open.RescueRect(MapLayout.SideOwn).GetCenter() + new Vec2(-8.0, 0.0);
            sim.AddPlayer(Player, 1);
            sim.Players[Player].Position = new Vec2(-50.0, -50.0);
            sim.AddDummy(Bot, 0, spot, FightBrain.Fighter(spot));
            sim.AddPlayer(3, 0);
            sim.Damage(3, 9999);
            Assert.IsTrue(sim.Players[3].Tagged);
            Kit.Wait(sim, 12.0);
            Assert.IsFalse(sim.Players[3].Tagged, "set free by the fighter");
        }

        [Test]
        public void TheSameSeedPlaysTheSameFight()
        {
            MatchSim first = Duel(5.0, "bato_light", "tsinelas_light", 99);
            MatchSim second = Duel(5.0, "bato_light", "tsinelas_light", 99);
            Kit.Wait(first, 6.0);
            Kit.Wait(second, 6.0);
            Assert.AreEqual(first.Players[Player].Hp, second.Players[Player].Hp);
            Assert.AreEqual(first.Players[Bot].Position, second.Players[Bot].Position);
        }

        [Test]
        public void AFighterIsStillADummyForCaptures()
        {
            MatchSim sim = Duel(5.0);
            Assert.IsTrue(sim.IsFighter(Bot));
            Assert.IsFalse(sim.Objectives.AllEnemiesTagged(sim, 0, sim.Brains), "a team of dummies never scores by being tagged");
        }

        // ---- aim, lead and cover ----

        static GameRules RulesWithoutError()
        {
            GameRules rules = Kit.Rules();
            rules.BotAimErrorDegrees = 0.0;
            return rules;
        }

        /// <summary>A bot (team 1) at the origin and an enemy (team 0) at `where`, with `speed` along x remembered from the last step.</summary>
        static (MatchSim sim, FightBrain brain, PlayerState me, PlayerState enemy) AimSetup(GameRules rules, Vec2 where, double speed, double skill)
        {
            var sim = new MatchSim(rules, OpenLayout(), 4);
            PlayerState enemy = sim.AddDummy(1, 0, where - new Vec2(speed / 30.0, 0.0), DummyBrain.Standing());
            FightBrain brain = FightBrain.Fighter(Vec2.Zero);
            brain.Skill = skill;
            PlayerState me = sim.AddDummy(2, 1, Vec2.Zero, brain);
            brain.Track(me, sim);
            enemy.Position = where;
            brain.Track(me, sim);
            return (sim, brain, me, enemy);
        }

        [Test]
        public void AGroundCircleIsThrownAtTheEnemysDistanceNotAtFullRange()
        {
            var made = AimSetup(RulesWithoutError(), new Vec2(0.0, 4.0), 0.0, 0.0);
            WeaponDef def = made.sim.WeaponDefs["bato_heavy"];
            Vec2 aim = made.brain.AimAt(made.me, made.enemy, def, made.sim);
            Assert.AreEqual(4.0 / def.MaxRange, aim.Length(), 0.01, "the circle lands where the enemy is");
            Assert.AreEqual(1.0, aim.Normalized().y, 0.01);
        }

        [Test]
        public void AThrownWeaponIsAimedAlongADirectionOfLengthOne()
        {
            var made = AimSetup(RulesWithoutError(), new Vec2(0.0, 5.0), 0.0, 0.0);
            Vec2 aim = made.brain.AimAt(made.me, made.enemy, made.sim.WeaponDefs["tsinelas_light"], made.sim);
            Assert.AreEqual(1.0, aim.Length(), 0.001);
        }

        [Test]
        public void ASkilledBotAimsAheadOfAMovingEnemyAndAnUnskilledOneDoesNot()
        {
            GameRules rules = RulesWithoutError();
            var plain = AimSetup(rules, new Vec2(0.0, 6.0), 5.0, 0.0);
            var sharp = AimSetup(rules, new Vec2(0.0, 6.0), 5.0, 1.0);
            WeaponDef def = plain.sim.WeaponDefs["tsinelas_light"];
            Vec2 atIt = plain.brain.AimAt(plain.me, plain.enemy, def, plain.sim);
            Vec2 ahead = sharp.brain.AimAt(sharp.me, sharp.enemy, def, sharp.sim);
            Assert.AreEqual(0.0, atIt.x, 0.001, "skill 0: straight at where it is");
            Assert.Greater(ahead.x, 0.1, "skill 1: toward where it is going");
        }

        [Test]
        public void ADelayedCircleIsLedByItsDelay()
        {
            var sharp = AimSetup(RulesWithoutError(), new Vec2(0.0, 6.0), 5.0, 1.0);
            WeaponDef def = sharp.sim.WeaponDefs["bato_heavy"];
            Vec2 landing = sharp.brain.AimAt(sharp.me, sharp.enemy, def, sharp.sim) * def.MaxRange;
            Assert.AreEqual(5.0 * def.Delay, landing.x, 0.2, "it lands where the enemy will be after the wind-up");
        }

        [Test]
        public void SkillComesFromTheRulesUnlessItIsSet()
        {
            var sim = new MatchSim(Rules, Kit.Layout, 1);
            FightBrain brain = FightBrain.Fighter(Vec2.Zero);
            Assert.AreEqual(Rules.BotSkill, brain.EffectiveSkill(sim));
            brain.Skill = 1.0;
            Assert.AreEqual(1.0, brain.EffectiveSkill(sim));
            brain.Skill = 7.0;
            Assert.AreEqual(1.0, brain.EffectiveSkill(sim), "clamped");
        }

        [Test]
        public void ASegmentIsStoppedByARectItCrossesAndNotByOneItMisses()
        {
            var wall = new Rect2(-2.0, -1.0, 4.0, 2.0);
            Assert.IsTrue(FightBrain.SegmentHitsRect(new Vec2(0.0, -5.0), new Vec2(0.0, 5.0), wall), "straight through");
            Assert.IsFalse(FightBrain.SegmentHitsRect(new Vec2(5.0, -5.0), new Vec2(5.0, 5.0), wall), "beside it");
            Assert.IsFalse(FightBrain.SegmentHitsRect(new Vec2(0.0, -5.0), new Vec2(0.0, -2.0), wall), "stops short");
            Assert.IsTrue(FightBrain.SegmentHitsRect(new Vec2(0.0, 0.0), new Vec2(0.0, 5.0), wall), "starts inside");
            Assert.IsTrue(FightBrain.SegmentHitsRect(new Vec2(-5.0, 0.0), new Vec2(5.0, 0.0), wall), "across, parallel to an edge");
            Assert.IsFalse(FightBrain.SegmentHitsRect(new Vec2(-5.0, 3.0), new Vec2(5.0, 3.0), wall), "parallel and outside");
        }

        [Test]
        public void ABotBehindCoverStepsAsideAndNeverThrowsIntoIt()
        {
            MapLayout layout = Kit.Layout.Duplicate();
            var wall = new Rect2(-3.0, -0.5, 6.0, 1.0);
            layout.Obstacles = new List<Rect2> { wall };
            layout.WaterObstacles = new List<Rect2>();
            var sim = new MatchSim(Rules, layout, 8);
            PlayerState target = sim.AddPlayer(Player, 0);
            target.Position = new Vec2(0.0, 5.0);
            var spot = new Vec2(0.0, -5.0);
            sim.AddDummy(Bot, 1, spot, FightBrain.Fighter(spot));
            sim.SetLoadout(Bot, "bato_light", "tsinelas_light");
            sim.SetLoadout(Player, "bola", "papel_shield");
            var badCasts = new List<int>();
            var casts = new List<string>();
            sim.WeaponCast += (id, weapon) =>
            {
                if (id != Bot) return;
                casts.Add(weapon);
                if (FightBrain.SegmentHitsRect(sim.Players[Bot].Position, target.Position, wall.Grow(0.3))) badCasts.Add(sim.Tick);
            };
            Kit.Wait(sim, 12.0);
            Assert.Greater(casts.Count, 0, "it found a way to throw");
            Assert.AreEqual(0, badCasts.Count, "never into the wall");
            Assert.Less(target.Hp, Rules.PlayerMaxHp, "and it landed");
        }
    }
}
