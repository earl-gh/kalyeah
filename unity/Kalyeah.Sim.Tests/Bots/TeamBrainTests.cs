using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Bots.BotKit;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>Port of tests/unit/test_team_brain.gd: a rusher walks into the enemy base and rescues when it is the one to; a defender holds its post.</summary>
    public class TeamBrainTests
    {
        static MapLayout Layout => Kit.Layout;

        static MatchSim Sim()
        {
            var sim = new MatchSim(Kit.Rules(), OpenLayout(), 9);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 0);
            sim.AddPlayer(3, 1);
            foreach (int id in new[] { 1, 2, 3 }) sim.SetLoadout(id, "bato_light", "papel_shield");
            return sim;
        }

        static Vec2 Spot(MatchSim sim) => Layout.SpawnCenter(-sim.SideForTeam(0));

        static TeamBrain Rusher(MatchSim sim) => TeamBrain.Rusher(sim.Players[1].SpawnPosition, Spot(sim));

        /// <summary>A fourth kid on team 0, tagged.</summary>
        static void TaggedTeammate(MatchSim sim)
        {
            sim.AddPlayer(4, 0);
            sim.SetLoadout(4, "bato_light", "papel_shield");
            sim.Kill(4);
        }

        [Test]
        public void ARusherWalksTowardTheEnemyBase()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            PlayerInput input = Rusher(sim).Think(sim.Players[1], sim);
            Vec2 way = (Spot(sim) - sim.Players[1].Position).Normalized();
            Assert.Greater(Way(input, sim.Players[1]).Dot(way), 0.99);
        }

        [Test]
        public void ARusherThatHasArrivedWalksAboutInTheEnemyBaseInsteadOfStanding()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            sim.Players[1].Position = Spot(sim);
            PlayerInput input = Rusher(sim).Think(sim.Players[1], sim);
            Assert.IsTrue(input.IsPressed(PlayerInput.BtnMoveTarget));
            Assert.IsTrue(Layout.InBase(-sim.SideForTeam(0), input.MoveTarget), "its next spot is in the enemy base too");
        }

        [Test]
        public void ADefenderHoldsItsPostUntilAnEnemyComes()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            sim.Players[1].Position = sim.Players[1].SpawnPosition + new Vec2(sim.Layout.BaseRadius + 3.0, 0.0);
            PlayerInput input = TeamBrain.Defender(sim.Players[1].SpawnPosition).Think(sim.Players[1], sim);
            Assert.Less(Way(input, sim.Players[1]).Dot(new Vec2(1.0, 0.0)), -0.99, "it walks back to its post");
        }

        [Test]
        public void ARusherThatMeetsAnEnemyStopsAndFightsItThenGoesOn()
        {
            MatchSim sim = Sim();
            sim.Players[1].Position = Vec2.Zero;
            sim.Players[3].Position = new Vec2(0.0, -4.0);
            TeamBrain brain = Rusher(sim);
            bool pressed = false;
            for (int i = 0; i < 6 && !pressed; i++) pressed = CardBits(brain.Think(sim.Players[1], sim)) != 0;
            Assert.IsTrue(pressed, "an enemy in reach is thrown at");
            Assert.AreEqual(0, CardBits(brain.Think(sim.Players[1], sim)), "the second half of the tap lets go");
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            PlayerInput next = brain.Think(sim.Players[1], sim);
            Assert.Greater(Way(next, sim.Players[1]).Dot((Spot(sim) - sim.Players[1].Position).Normalized()), 0.99, "with the enemy gone it goes on");
        }

        [Test]
        public void PriorityOneAnEnemyInReachIsFoughtBeforeTheBaseAndBeforeARescue()
        {
            MatchSim sim = Sim();
            TaggedTeammate(sim);
            Vec2 centre = Layout.BaseCenter(-sim.SideForTeam(0));
            Vec2 outward = (Vec2.Zero - centre).Normalized();
            sim.Players[1].Position = centre + outward * (Layout.BaseRadius + 1.5);
            sim.Players[3].Position = sim.Players[1].Position + outward * 22.0;
            PlayerInput input = Rusher(sim).Think(sim.Players[1], sim);
            Assert.Greater(Way(input, sim.Players[1]).Dot(outward), 0.9, "it closes on the enemy instead of holding the base or leaving for the box");
        }

        [Test]
        public void InTheEnemyBaseNothingCanBeCastSoARusherStepsOutToFight()
        {
            MatchSim sim = Sim();
            Vec2 centre = Layout.BaseCenter(-sim.SideForTeam(0));
            Vec2 outward = (Vec2.Zero - centre).Normalized();
            sim.Players[1].Position = centre + outward * 1.0;
            sim.Players[3].Position = centre + outward * 9.0;
            Assert.IsTrue(sim.CastingBlocked(sim.Players[1]), "inside the base it cannot cast");
            PlayerInput input = Rusher(sim).Think(sim.Players[1], sim);
            Assert.Greater(Way(input, sim.Players[1]).Dot(outward), 0.9, "it walks out of the base toward the fight");
        }

        [Test]
        public void ATiredBotInAFightKeepsMovingItNeverStandsToRefill()
        {
            MatchSim sim = Sim();
            TeamBrain brain = TeamBrain.Defender(sim.Players[1].SpawnPosition);
            sim.Players[1].Position = new Vec2(0.0, 6.0);
            sim.Players[1].Elixir = 0.0;
            FillCooldowns(sim.Players[1], 99.0);
            double standoff = brain.Standoff(sim.Players[1], sim);
            sim.Players[3].Position = new Vec2(0.0, 6.0 - standoff);
            sim.Players[1].Stamina = 1.0;
            PlayerInput tired = brain.Think(sim.Players[1], sim);
            Assert.IsTrue(tired.IsPressed(PlayerInput.BtnMoveTarget));
            Assert.Greater(tired.MoveTarget.DistanceTo(sim.Players[1].Position), 0.5, "low on stamina it still strafes");
        }

        [Test]
        public void PriorityTwoTheBaseComesBeforeARescueSoARusherNotThereYetKeepsGoing()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            TaggedTeammate(sim);
            PlayerInput input = Rusher(sim).Think(sim.Players[1], sim);
            Assert.Greater(Way(input, sim.Players[1]).Dot((Spot(sim) - sim.Players[1].Position).Normalized()), 0.99, "to their base first");
        }

        [Test]
        public void PriorityThreeFromTheEnemyBaseTheLowestNumberedFreeRusherFetchesATaggedTeammate()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            TaggedTeammate(sim);
            Vec2 area = Layout.RescueRect(sim.SideForTeam(0)).GetCenter();
            sim.Players[1].Position = Spot(sim);
            sim.Players[2].Position = Spot(sim);
            TeamBrain first = TeamBrain.Rusher(sim.Players[1].SpawnPosition, Spot(sim));
            TeamBrain second = TeamBrain.Rusher(sim.Players[2].SpawnPosition, Spot(sim));
            PlayerInput one = first.Think(sim.Players[1], sim);
            PlayerInput two = second.Think(sim.Players[2], sim);
            Assert.Greater(Way(one, sim.Players[1]).Dot((area - sim.Players[1].Position).Normalized()), 0.99, "number 1 goes to the rescue area");
            Assert.IsTrue(Layout.InBase(-sim.SideForTeam(0), two.MoveTarget), "number 2 stays in the base, walking about in it");
        }

        [Test]
        public void ARescueThatHasStartedIsNotCalledOffByLeavingTheBase()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            TaggedTeammate(sim);
            Vec2 area = Layout.RescueRect(sim.SideForTeam(0)).GetCenter();
            sim.Players[1].Position = Spot(sim);
            TeamBrain brain = Rusher(sim);
            brain.Think(sim.Players[1], sim);
            sim.Players[1].Position = (Spot(sim) + area) / 2.0;
            PlayerInput input = brain.Think(sim.Players[1], sim);
            Assert.Greater(Way(input, sim.Players[1]).Dot((area - sim.Players[1].Position).Normalized()), 0.99, "it keeps going to the rescue area");
        }

        [Test]
        public void ADefenderAtItsPostFetchesATaggedTeammateOnlyFromThePost()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            TaggedTeammate(sim);
            Vec2 area = Layout.RescueRect(sim.SideForTeam(0)).GetCenter();
            TeamBrain away = TeamBrain.Defender(sim.Players[1].SpawnPosition);
            sim.Players[1].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            PlayerInput wandering = away.Think(sim.Players[1], sim);
            Assert.Greater(Way(wandering, sim.Players[1]).Dot((sim.Players[1].SpawnPosition - sim.Players[1].Position).Normalized()), 0.99,
                "a defender out of its base goes home first");
            TeamBrain home = TeamBrain.Defender(sim.Players[1].SpawnPosition);
            sim.Players[1].Position = sim.Players[1].SpawnPosition;
            PlayerInput input = home.Think(sim.Players[1], sim);
            Assert.Greater(Way(input, sim.Players[1]).Dot((area - sim.Players[1].Position).Normalized()), 0.99, "from its post it goes for the rescue area");
        }

        [Test]
        public void BotsWalkToTheRescueAreaAndFreeATaggedTeammate()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            TaggedTeammate(sim);
            var brains = new Dictionary<int, TeamBrain>
            {
                [1] = TeamBrain.Defender(sim.Players[1].SpawnPosition), [2] = TeamBrain.Defender(sim.Players[2].SpawnPosition),
            };
            bool freed = false;
            for (int i = 0; i < 30 * 40 && !freed; i++)
            {
                foreach (var pair in brains) sim.SetInput(pair.Key, pair.Value.Think(sim.Players[pair.Key], sim));
                sim.Step(DT);
                freed = !sim.Players[4].Tagged;
            }
            Assert.IsTrue(freed, "a bot stood in the rescue area for the hold time and freed the kid on the sidewalk");
        }

        [Test]
        public void ABotWaitingAtItsPostWalksAboutInItsBaseAndNeverStands()
        {
            MatchSim sim = Sim();
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            int home = sim.SideForTeam(0);
            TeamBrain brain = TeamBrain.Defender(sim.Players[1].SpawnPosition);
            PlayerState state = sim.Players[1];
            state.Position = Layout.BaseCenter(home);
            var spots = new HashSet<Vec2>();
            for (int i = 0; i < 240; i++)
            {
                PlayerInput input = brain.Think(state, sim);
                Assert.IsTrue(input.IsPressed(PlayerInput.BtnMoveTarget));
                Assert.Greater(input.MoveTarget.DistanceTo(state.Position), 0.05, $"tick {i}: it has somewhere to go");
                Assert.IsTrue(Layout.InBase(home, input.MoveTarget), "and stays in its own base");
                spots.Add(input.MoveTarget);
                state.Position = state.Position.MoveToward(input.MoveTarget, 5.0 * DT);
            }
            Assert.Greater(spots.Count, 2, "over eight seconds it walks to several places");
        }

        [Test]
        public void ABaseHolderStaysInTheEnemyBaseEvenWithAnEnemyNextToIt()
        {
            MatchSim sim = Sim();
            int enemySide = -sim.SideForTeam(0);
            Vec2 centre = Layout.BaseCenter(enemySide);
            Vec2 outward = (Vec2.Zero - centre).Normalized();
            sim.Players[1].Position = centre;
            sim.Players[3].Position = centre + outward * 5.0;
            TeamBrain brain = Rusher(sim);
            brain.Hold = true;
            for (int i = 0; i < 90; i++)
            {
                PlayerInput input = brain.Think(sim.Players[1], sim);
                Assert.IsTrue(Layout.InBase(enemySide, input.MoveTarget), $"tick {i}: it never sets off out of the base");
                sim.Players[1].Position = sim.Players[1].Position.MoveToward(input.MoveTarget, 5.0 * DT);
                Assert.IsTrue(Layout.InBase(enemySide, sim.Players[1].Position));
            }
        }

        [Test]
        public void AFightDoesNotWalkIntoTheEnemyBaseItGoesAlongItsEdge()
        {
            MatchSim sim = Sim();
            int enemySide = -sim.SideForTeam(0);
            Vec2 centre = Layout.BaseCenter(enemySide);
            Vec2 outward = (Vec2.Zero - centre).Normalized();
            Vec2 edge = centre;
            while (Layout.InBase(enemySide, edge)) edge += outward * 0.1;
            edge += outward * 0.3;
            sim.Players[1].Position = edge;
            sim.Players[1].Elixir = 0.0;
            FillCooldowns(sim.Players[1], 99.0);
            sim.Players[3].Position = centre;
            TeamBrain brain = Rusher(sim);
            for (int i = 0; i < 60; i++)
            {
                PlayerInput input = brain.Think(sim.Players[1], sim);
                Vec2 step = input.MoveTarget - sim.Players[1].Position;
                sim.Players[1].Position += step.LimitLength(5.0 * DT);
                Assert.IsFalse(sim.CastingBlocked(sim.Players[1]), $"tick {i}: it stays out of the place it cannot cast in");
            }
        }

        [Test]
        public void AfterSteppingOutToFightARusherWaitsOutsideBeforeGoingBackIn()
        {
            MatchSim sim = Sim();
            int enemySide = -sim.SideForTeam(0);
            Vec2 centre = Layout.BaseCenter(enemySide);
            Vec2 outward = (Vec2.Zero - centre).Normalized();
            sim.Players[1].Position = centre + outward * 1.0;
            sim.Players[3].Position = centre + outward * 9.0;
            TeamBrain brain = Rusher(sim);
            brain.Think(sim.Players[1], sim);  // steps out
            sim.Players[1].Position = centre + outward * (Layout.BaseRadius + 1.0);
            sim.Players[3].Position = new Vec2(200.0, 200.0);
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(Layout.InBase(enemySide, brain.Think(sim.Players[1], sim).MoveTarget), $"tick {i}: it does not walk straight back in");
            for (int i = 0; i < 90; i++) brain.Think(sim.Players[1], sim);
            Assert.IsTrue(Layout.InBase(enemySide, brain.Think(sim.Players[1], sim).MoveTarget), "later it goes back in");
        }
    }
}
