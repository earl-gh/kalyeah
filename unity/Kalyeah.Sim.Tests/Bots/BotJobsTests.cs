using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Bots.BotKit;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>
    /// Port of tests/unit/test_bot_jobs.gd: the jobs a TeamCoach gives single bots (rescuer, guard, pusher), and whom a bot fights first.
    /// </summary>
    public class BotJobsTests
    {
        GameRules _rules;
        static MapLayout Layout => Kit.Layout;

        [SetUp]
        public void BeforeEach() => _rules = Kit.Rules();

        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, Layout, 31);
            foreach (int id in new[] { 1, 2, 3 }) sim.AddPlayer(id, 0);
            foreach (int id in new[] { 4, 5, 6 }) sim.AddPlayer(id, 1);
            foreach (int id in sim.Players.KeyList()) sim.SetLoadout(id, "bato_light", "papel_shield");
            return sim;
        }

        static TeamCoach Allies(MatchSim sim)
        {
            var coach = new TeamCoach(0, 1);
            foreach (int id in new[] { 2, 3 }) coach.Add(sim, id, 0.4);
            return coach;
        }

        static TeamCoach Enemies(MatchSim sim)
        {
            var coach = new TeamCoach(1);
            foreach (int id in new[] { 4, 5, 6 }) coach.Add(sim, id, 0.4);
            return coach;
        }

        static Vec2 Area(MatchSim sim, int team) => Layout.RescueRect(sim.SideForTeam(team)).GetCenter();

        static void EnemiesAway(MatchSim sim)
        {
            foreach (int id in new[] { 4, 5, 6 }) sim.Players[id].Position = Layout.BaseCenter(sim.SideForTeam(1));
        }

        [Test]
        public void AnAllyBotRescuesATaggedAllyWhileYouAreFree()
        {
            MatchSim sim = Sim();
            sim.RescueReadyLeft = new[] { 0.0, 0.0 };
            EnemiesAway(sim);
            TeamCoach coach = Allies(sim);
            sim.Kill(3);
            sim.Players[2].Position = Area(sim, 0) + new Vec2(0.0, 4.0);
            Assert.AreEqual(2, coach.RescuerId(sim), "a bot, though you (id 1) are free and numbered lower");
            Run(sim, coach, 12.0);
            Assert.IsFalse(sim.Players[3].Tagged, "bot 2 walked over and freed bot 3");
        }

        [Test]
        public void TheRescuerIsTheFreeBotNearestTheRescueArea()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            sim.Kill(4);
            sim.Players[5].Position = Area(sim, 1) + new Vec2(0.0, 10.0);
            sim.Players[6].Position = Area(sim, 1) + new Vec2(0.0, 2.0);
            Assert.AreEqual(6, coach.RescuerId(sim), "not the lowest number: the nearest");
            sim.Objectives.Rescue(sim, sim.Players[4], 6);
            Assert.AreEqual(-1, coach.RescuerId(sim), "nobody tagged, no rescuer");
        }

        [Test]
        public void TheRescuerWaitsForTheCountdownAndSetsOffToArriveAsItEnds()
        {
            MatchSim sim = Sim();
            EnemiesAway(sim);
            TeamCoach coach = Allies(sim);
            sim.Kill(3);
            Vec2 start = Area(sim, 0) + new Vec2(0.0, 6.0);
            sim.Players[2].Position = start;
            sim.RescueReadyLeft = new[] { 9.0, 9.0 };
            coach.Drive(sim);
            sim.Step(DT);
            Assert.GreaterOrEqual(sim.Players[2].Position.DistanceTo(Area(sim, 0)), start.DistanceTo(Area(sim, 0)) - 0.01,
                "9 s left and 1.2 s of walk: it does not go and stand in the area yet");
            sim.RescueReadyLeft = new[] { 1.5, 1.5 };
            Run(sim, coach, 0.5);
            Assert.Less(sim.Players[2].Position.DistanceTo(Area(sim, 0)), start.DistanceTo(Area(sim, 0)), "now it sets off");
        }

        [Test]
        public void ARescuerInTheAreaHoldsItWhenAnEnemyComesNear()
        {
            MatchSim sim = Sim();
            sim.RescueReadyLeft = new[] { 0.0, 0.0 };
            EnemiesAway(sim);
            TeamCoach coach = Allies(sim);
            sim.Kill(3);
            sim.Players[2].Position = Area(sim, 0);
            Run(sim, coach, 1.0);
            sim.Players[4].Position = Area(sim, 0) + new Vec2(2.0, 0.0);
            Run(sim, coach, 1.0);
            Assert.IsTrue(sim.Objectives.InRescueArea(sim, sim.Players[2]), "it stays in the area instead of stepping out to fight");
        }

        [Test]
        public void AGuardGoesHomeWhileTheBaseIsTakenDuringAnAttack()
        {
            _rules.BotAssaultDelay = 0.0;
            _rules.BotAssaultWindow = 0.0;
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            Run(sim, coach, 1.0);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan);
            Assert.AreEqual(-1, coach.GuardId(sim, -1), "nobody is taking their base");
            sim.Players[1].Position = Layout.BaseCenter(sim.SideForTeam(1));
            sim.Step(DT);
            Assert.AreEqual(0, sim.CaptureTeam);
            int guard = coach.GuardId(sim, -1);
            Assert.Greater(guard, 0, "one of them is sent home");
            coach.Drive(sim);
            Assert.AreEqual(TeamBrain.BrainRole.Defend, coach.Brains[guard].Role);
            foreach (int id in new[] { 4, 5, 6 })
                if (id != guard) Assert.AreEqual(TeamBrain.BrainRole.Rush, coach.Brains[id].Role, "the others keep attacking");
        }

        [Test]
        public void AnAllyPushesAloneOnceMostEnemiesAreTagged()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Allies(sim);
            coach.Drive(sim);
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, coach.Plan, "you are at home, so they defend");
            Assert.AreEqual(-1, coach.PusherId(sim, -1), "every enemy is free: nobody pushes");
            sim.Kill(4);
            Assert.AreEqual(-1, coach.PusherId(sim, -1), "two of three free is not safe yet");
            sim.Kill(5);
            int pusher = coach.PusherId(sim, -1);
            Assert.IsTrue(pusher == 2 || pusher == 3, "one ally goes");
            coach.Drive(sim);
            Assert.AreEqual(TeamBrain.BrainRole.Rush, coach.Brains[pusher].Role);
            Assert.AreEqual(TeamBrain.BrainRole.Defend, coach.Brains[5 - pusher].Role, "the other stays home");
        }

        [Test]
        public void ATeamWithoutAHumanNeverHasAPusher()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            sim.Kill(1);
            sim.Kill(2);
            coach.Drive(sim);
            Assert.AreEqual(-1, coach.PusherId(sim, -1), "the enemies plan their attacks with the clock");
        }

        PlayerState Picker(MatchSim sim, int id) =>
            FightBrain.Fighter(sim.Players[id].Position).PickEnemy(sim.Players[id], sim, _rules.BotAggroRange);

        [Test]
        public void TheEnemyTakingTheBaseIsFoughtBeforeANearerOne()
        {
            MatchSim sim = Sim();
            Vec2 home = Layout.BaseCenter(sim.SideForTeam(1));
            double outward = -sim.SideForTeam(1);
            sim.Players[4].Position = home + new Vec2(0.0, (Layout.BaseRadius + 2.0) * outward);
            sim.Players[2].Position = sim.Players[4].Position + new Vec2(0.0, 1.5 * outward);
            sim.Players[1].Position = home;
            Assert.IsFalse(Layout.InBase(sim.SideForTeam(1), sim.Players[2].Position));
            foreach (int id in new[] { 3, 5, 6 }) sim.Players[id].Position = new Vec2(100.0, 0.0);
            Assert.AreEqual(1, Picker(sim, 4).Id, "you, in their base, before ally 2 standing right beside them");
        }

        [Test]
        public void ARescuerThenAHurtEnemyComeBeforeTheNearest()
        {
            MatchSim sim = Sim();
            foreach (int id in new[] { 2, 3, 5, 6 }) sim.Players[id].Position = new Vec2(100.0, 0.0);
            sim.Players[4].Position = Vec2.Zero;
            sim.Players[1].Position = new Vec2(2.0, 0.0);
            sim.Players[2].Position = new Vec2(6.0, 0.0);
            sim.Players[3].Position = new Vec2(-8.0, 0.0);
            Assert.AreEqual(1, Picker(sim, 4).Id, "nobody special: the nearest");
            sim.Players[2].Hp = 20;
            Assert.AreEqual(2, Picker(sim, 4).Id, "a hurt one before the nearest");
            sim.RescueRescuer[0] = 3;
            Assert.AreEqual(3, Picker(sim, 4).Id, "the one rescuing before a hurt one");
        }
    }

    /// <summary>Port of tests/unit/test_bot_rules.gd: bots play by the players' rules (move targets, affordable cards, released at a street point).</summary>
    public class BotRulesTests
    {
        GameRules _rules;

        [SetUp]
        public void BeforeEach()
        {
            _rules = Kit.Rules();
            _rules.CardCycle = true;
            _rules.ElixirEnabled = true;
            _rules.StaminaEnabled = true;
            _rules.HandSize = 4;
            _rules.PrimaryWeapon = null;
            _rules.UltimateWeapon = null;
        }

        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, Kit.Layout, 9);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 1);
            sim.Equip(1);
            sim.Equip(2);
            sim.Players[2].Position = new Vec2(12.0, -22.0);
            return sim;
        }

        static TeamBrain Rusher(MatchSim sim) => TeamBrain.Rusher(sim.Players[1].SpawnPosition, Kit.Layout.SpawnCenter(-sim.SideForTeam(0)));

        [Test]
        public void ABotWalksByMoveTargetsNotAStick()
        {
            MatchSim sim = Sim();
            PlayerInput input = Rusher(sim).Think(sim.Players[1], sim);
            Assert.IsTrue(input.IsPressed(PlayerInput.BtnMoveTarget));
            Assert.AreEqual(Vec2.Zero, input.Move);
        }

        [Test]
        public void ABotLowOnStaminaWithNoEnemyNearKeepsWalkingOn()
        {
            MatchSim sim = Sim();
            PlayerState state = sim.Players[1];
            state.Position = new Vec2(0.0, 5.0);
            state.Stamina = 1.0;
            Assert.AreNotEqual(state.Position, Rusher(sim).Think(state, sim).MoveTarget, "it does not stop to refill");
        }

        [Test]
        public void ATiredBotStillFightsAnEnemyInReach()
        {
            MatchSim sim = Sim();
            TeamBrain brain = Rusher(sim);
            PlayerState state = sim.Players[1];
            state.Position = new Vec2(0.0, 5.0);
            state.Stamina = 1.0;
            state.Elixir = _rules.ElixirMax;
            sim.Players[2].Position = new Vec2(0.0, 1.0);
            int cards = 0;
            for (int i = 0; i < 4; i++) cards |= CardBits(brain.Think(state, sim));
            Assert.AreNotEqual(0, cards, "an enemy within bot_aggro_range is fought, tired or not");
        }

        [Test]
        public void ABotNeverPicksUpACardItCannotAfford()
        {
            MatchSim sim = Sim();
            TeamBrain brain = Rusher(sim);
            PlayerState state = sim.Players[1];
            state.Position = new Vec2(0.0, 5.0);
            sim.Players[2].Position = new Vec2(0.0, 2.0);
            state.Elixir = 1.0;
            _rules.ElixirRegenPerS = 0.0;
            var casts = new List<int>();
            sim.WeaponCast += (id, weapon) => casts.Add(id);
            for (int i = 0; i < 60; i++)
            {
                sim.SetInput(1, brain.Think(state, sim));
                sim.Step(DT);
            }
            CollectionAssert.DoesNotContain(casts, 1, "1 elixir buys no card (the cheapest costs 2)");
        }

        [Test]
        public void ABotReleasesAGroundCardAtAStreetPoint()
        {
            MatchSim sim = Sim();
            TeamBrain brain = Rusher(sim);
            PlayerState state = sim.Players[1];
            state.Position = new Vec2(0.0, 5.0);
            state.Elixir = _rules.ElixirMax;
            state.Weapons = new List<string> { "lata", "lata", "lata", "lata" };
            sim.Players[2].Position = new Vec2(0.0, 1.0);
            PlayerInput release = null;
            for (int i = 0; i < 6 && release == null; i++)
            {
                PlayerInput input = brain.Think(state, sim);
                if (input.IsPressed(PlayerInput.BtnAimPoint)) release = input;
            }
            Assert.IsNotNull(release, "the release carries an aim point");
            Assert.Less(release.AimPoint.DistanceTo(sim.Players[2].Position), 2.0, "near the enemy");
        }
    }

    /// <summary>Port of the sim parts of tests/unit/test_bot_levels.gd (GameRules.WithBotLevel feeds the bots).</summary>
    public class BotLevelsTests
    {
        [Test]
        public void ThereAreThreeLevelsInOrderAndMediumIsTheDefault()
        {
            GameRules rules = Kit.Rules();
            var ids = new List<string>();
            foreach (BotLevel level in rules.BotLevels)
            {
                ids.Add(level.Id);
                Assert.IsNotEmpty(level.Label);
                Assert.IsNotEmpty(level.Blurb);
            }
            CollectionAssert.AreEqual(new[] { "easy", "medium", "hard" }, ids);
            Assert.AreEqual("medium", rules.BotLevelDefault);
        }

        [Test]
        public void MediumIsTheNumbersTheBotsAlwaysHad()
        {
            GameRules rules = Kit.Rules();
            GameRules medium = rules.WithBotLevel("medium");
            Assert.AreEqual(rules.BotSkill, medium.BotSkill);
            Assert.AreEqual(rules.BotAimErrorDegrees, medium.BotAimErrorDegrees);
            Assert.AreEqual(rules.BotCastGap, medium.BotCastGap);
            Assert.AreEqual(rules.BotAggroRange, medium.BotAggroRange);
            Assert.AreEqual(rules.BotAssaultDelay, medium.BotAssaultDelay);
            Assert.AreEqual(rules.BotAssaultWindow, medium.BotAssaultWindow);
            Assert.AreEqual(rules.BotRestTime, medium.BotRestTime);
        }

        [Test]
        public void EachStepUpIsHarderInEveryNumber()
        {
            GameRules rules = Kit.Rules();
            BotLevel easy = rules.GetBotLevel("easy");
            BotLevel medium = rules.GetBotLevel("medium");
            BotLevel hard = rules.GetBotLevel("hard");
            Assert.Less(easy.Skill, medium.Skill);
            Assert.Less(medium.Skill, hard.Skill);
            Assert.Greater(easy.AimErrorDegrees, medium.AimErrorDegrees);
            Assert.Greater(medium.AimErrorDegrees, hard.AimErrorDegrees);
            Assert.Greater(easy.CastGap, medium.CastGap);
            Assert.Greater(medium.CastGap, hard.CastGap);
            Assert.Less(easy.AggroRange, medium.AggroRange);
            Assert.Less(medium.AggroRange, hard.AggroRange);
            Assert.Greater(easy.AssaultDelay, medium.AssaultDelay);
            Assert.Greater(medium.AssaultDelay, hard.AssaultDelay);
            Assert.Less(easy.AssaultWindow, medium.AssaultWindow);
            Assert.Less(medium.AssaultWindow, hard.AssaultWindow);
            Assert.Greater(easy.RestTime, medium.RestTime);
            Assert.Greater(medium.RestTime, hard.RestTime);
        }

        [Test]
        public void ALevelIsACopyAndNeverChangesTheSharedRules()
        {
            GameRules rules = Kit.Rules();
            double skill = rules.BotSkill;
            GameRules hard = rules.WithBotLevel("hard");
            Assert.AreEqual(rules.GetBotLevel("hard").Skill, hard.BotSkill);
            Assert.AreEqual(skill, rules.BotSkill, "the shared rules keep their own numbers");
            Assert.AreNotSame(rules, hard);
            Assert.AreEqual(rules.GetBotLevel("medium").Skill, rules.WithBotLevel("nonsense").BotSkill, "an unknown level plays as the default");
        }
    }
}
