using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Bots.BotKit;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>Port of tests/unit/test_team_coach.gd (the sim parts; the PracticeMatch scene tests stay in GUT).</summary>
    public class TeamCoachTests
    {
        GameRules _rules;
        static MapLayout Layout => Kit.Layout;

        [SetUp]
        public void BeforeEach() => _rules = Kit.Rules();

        /// <summary>You (id 1) and two allies (2, 3) against three enemies (4, 5, 6); everyone armed.</summary>
        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, Layout, 12);
            foreach (int id in new[] { 1, 2, 3 }) sim.AddPlayer(id, 0);
            foreach (int id in new[] { 4, 5, 6 }) sim.AddPlayer(id, 1);
            foreach (int id in sim.Players.KeyList()) sim.SetLoadout(id, "bato_light", "papel_shield", "lata");
            return sim;
        }

        static TeamCoach Enemies(MatchSim sim)
        {
            var coach = new TeamCoach(1);
            foreach (int id in new[] { 4, 5, 6 }) coach.Add(sim, id, 0.4);
            return coach;
        }

        static TeamCoach Allies(MatchSim sim)
        {
            var coach = new TeamCoach(0, 1);
            foreach (int id in new[] { 2, 3 }) coach.Add(sim, id, 0.4);
            return coach;
        }

        [Test]
        public void BotsAreRealPlayersWhoCountForACapture()
        {
            MatchSim sim = Sim();
            var scored = new List<int>();
            sim.PointScored += (team, how) => scored.Add(team);
            Vec2 baseCenter = Layout.BaseCenter(MapLayout.SideEnemy);
            foreach (int id in new[] { 1, 2, 3 }) sim.Players[id].Position = baseCenter + new Vec2(id - 2.0, 0.0);
            foreach (int id in new[] { 4, 5, 6 }) sim.Players[id].Position = new Vec2((id - 5) * 2.0, 0.0);
            Kit.Wait(sim, _rules.CaptureSoloTime + 1.0);
            CollectionAssert.AreEqual(new[] { 0 }, scored, "three free players in the enemy base are a capture whether or not a brain drives them");
        }

        [Test]
        public void TheEnemyTeamDefendsUntilTheDelayAndThenAttacks()
        {
            _rules.BotAssaultDelay = 10.0;
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            Run(sim, coach, 5.0);
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, coach.Plan);
            Run(sim, coach, 6.0);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan);
        }

        [Test]
        public void WithTheRealNumbersTheEnemiesComeOutRightAfterGoAndRestBriefly()
        {
            Assert.LessOrEqual(_rules.BotAssaultDelay, 3.0);
            Assert.LessOrEqual(_rules.BotRestTime, 8.0);
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            Vec2 start = sim.Players[4].Position;
            Run(sim, coach, _rules.BotAssaultDelay + 2.0);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan, "attacking a couple of seconds after GO");
            Assert.Greater(sim.Players[4].Position.DistanceTo(start), 2.0, "and out of their base");
        }

        [Test]
        public void ATeamWithMostOfItTaggedFallsBackToDefending()
        {
            _rules.BotAssaultDelay = 0.0;
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            Run(sim, coach, 1.0);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan);
            sim.Kill(4);
            Run(sim, coach, 0.2);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan, "one of three tagged: still two thirds free");
            sim.Kill(5);
            Run(sim, coach, 0.2);
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, coach.Plan);
        }

        [Test]
        public void AlliesDefendUntilTheHumanIsInTheEnemyHalfAndThenBackThemUp()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Allies(sim);
            sim.Players[1].Position = Layout.SpawnCenter(MapLayout.SideOwn);
            coach.Drive(sim);
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, coach.Plan);
            sim.Players[1].Position = Layout.BaseCenter(MapLayout.SideEnemy);
            coach.Drive(sim);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan);
            sim.Kill(1);
            coach.Drive(sim);
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, coach.Plan, "a tagged human: the allies go back to fetch them");
        }

        [Test]
        public void AlliesComeOutOnTheClockLikeTheEnemiesWhileYouStayHome()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Allies(sim);
            Vec2 start = sim.Players[2].Position;
            Run(sim, coach, _rules.BotAssaultDelay + 3.0);
            Assert.IsTrue(Layout.InBase(MapLayout.SideOwn, sim.Players[1].Position), "you never left your base");
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan, "they attack on the same clock as the enemies");
            Assert.Greater(sim.Players[2].Position.DistanceTo(start), 2.0, "and are on their way");
        }

        [Test]
        public void TheEnemyPlanWorksFromTheOtherSideOfTheStreet()
        {
            MatchSim sim = Sim();
            var coach = new TeamCoach(1, 4);
            coach.Add(sim, 5, 0.4);
            sim.Players[4].Position = Layout.BaseCenter(MapLayout.SideOwn);
            coach.Drive(sim);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan, "team 1's enemy half is the +Z end");
        }

        [Test]
        public void DrivenBotsWalkAndTaggedOnesAreLeftAlone()
        {
            _rules.BotAssaultDelay = 0.0;
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            Vec2 before = sim.Players[4].Position;
            Run(sim, coach, 2.0);
            Assert.AreNotEqual(before, sim.Players[4].Position, "the attack is under way");
            sim.Kill(6);
            Vec2 box = sim.Players[6].Position;
            Run(sim, coach, 1.0);
            Assert.AreEqual(box, sim.Players[6].Position, "a tagged bot stays where it stands");
        }

        [Test]
        public void NoBotIsDrivenOutsidePlay()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            sim.BeginIntro();
            Vec2 before = sim.Players[4].Position;
            coach.Drive(sim);
            sim.Step(DT);
            Assert.AreEqual(before, sim.Players[4].Position);
        }

        [Test]
        public void ABotMatchWithNobodyAtTheControlsEndsWithAPoint()
        {
            _rules.BotAssaultDelay = 5.0;
            MatchSim sim = Sim();
            var scored = new List<int>();
            sim.PointScored += (team, how) => scored.Add(team);
            var coaches = new[] { Enemies(sim), Allies(sim) };
            for (int i = 0; i < 30 * 150 && scored.Count == 0; i++)
            {
                foreach (TeamCoach coach in coaches) coach.Drive(sim);
                sim.Step(DT);
            }
            Assert.IsNotEmpty(scored, "within two and a half minutes someone has scored");
        }

        [Test]
        public void TheEnemyAttacksInWavesAndRestsBetweenThem()
        {
            _rules.BotAssaultDelay = 10.0;
            _rules.BotAssaultWindow = 8.0;
            _rules.BotRestTime = 6.0;
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            var seen = new Dictionary<int, TeamCoach.CoachPlan>();
            for (int second = 0; second < 40; second++)
            {
                Run(sim, coach, 1.0);
                seen[second] = coach.Plan;
            }
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, seen[5], "waiting");
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, seen[12], "first wave (10 to 18 s)");
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, seen[20], "resting (18 to 24 s)");
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, seen[27], "second wave (24 to 32 s)");
            Assert.AreEqual(TeamCoach.CoachPlan.Defend, seen[35], "resting again");
        }

        [Test]
        public void AWindowOfZeroMeansTheEnemyNeverStopsAttacking()
        {
            _rules.BotAssaultDelay = 0.0;
            _rules.BotAssaultWindow = 0.0;
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            Run(sim, coach, 60.0);
            Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan);
        }

        [Test]
        public void WhileAttackingTheSetNumberOfAttackersHoldTheEnemyBaseAndKeepTheJob()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            sim.RoundTime = _rules.BotAssaultDelay + 1.0;
            foreach (int holders in new[] { 0, 1, 2 })
            {
                _rules.BotBaseHolders = holders;
                coach.Holders.Clear();
                coach.Drive(sim);
                Assert.AreEqual(TeamCoach.CoachPlan.Assault, coach.Plan);
                List<int> held = coach.Members().Where(id => coach.Brains[id].Hold).ToList();
                Assert.AreEqual(holders, held.Count, $"{holders} base holders asked for");
                for (int i = 0; i < 60; i++)
                {
                    coach.Drive(sim);
                    sim.Step(DT);
                    sim.RoundTime = _rules.BotAssaultDelay + 1.0;
                }
                List<int> after = coach.Members().Where(id => coach.Brains[id].Hold).ToList();
                CollectionAssert.AreEqual(held, after, "the job stays with the same bots");
            }
        }

        [Test]
        public void DefendersHoldNothing()
        {
            MatchSim sim = Sim();
            TeamCoach coach = Enemies(sim);
            sim.RoundTime = 0.5;
            coach.Drive(sim);
            foreach (int id in coach.Members()) Assert.IsFalse(coach.Brains[id].Hold);
        }

        /// <summary>Both teams of bots play ninety seconds: a holder never steps out of the base it holds, and no free bot stands still for long.</summary>
        [Test]
        public void BotsNeverStandAroundAndHoldersNeverBlinkTheCapture()
        {
            var sim = new MatchSim(_rules, Layout, 31);
            var ours = new TeamCoach(0);
            var theirs = new TeamCoach(1);
            foreach (int id in new[] { 1, 2, 3 })
            {
                sim.AddPlayer(id, 0);
                ours.Add(sim, id, 0.4);
            }
            foreach (int id in new[] { 4, 5, 6 })
            {
                sim.AddPlayer(id, 1);
                theirs.Add(sim, id, 0.4);
            }
            sim.AssignCharacters();
            foreach (int id in sim.Players.KeyList()) sim.Equip(id, true);
            var still = new Dictionary<int, int>();
            int longest = 0;
            int steppedOut = 0;
            for (int tick = 0; tick < 2700; tick++)
            {
                if (sim.Phase == MatchSim.MatchPhase.MatchOver) break;
                ours.Drive(sim);
                theirs.Drive(sim);
                var before = new Dictionary<int, Vec2>();
                var heldIn = new Dictionary<int, bool>();
                foreach (var pair in sim.Players)
                {
                    PlayerState state = pair.Value;
                    before[pair.Key] = state.Position;
                    TeamBrain brain = (state.Team == 0 ? ours : theirs).Brains[pair.Key];
                    heldIn[pair.Key] = brain.Hold && !state.Tagged && Layout.InBase(-sim.SideForTeam(state.Team), state.Position);
                }
                sim.Step(DT);
                if (sim.Phase != MatchSim.MatchPhase.Playing)
                {
                    still.Clear();
                    continue;
                }
                foreach (var pair in sim.Players)
                {
                    int id = pair.Key;
                    PlayerState state = pair.Value;
                    bool walked = state.Position.DistanceTo(before[id]) < 1.0;
                    if (heldIn[id] && walked && !state.Tagged && !Layout.InBase(-sim.SideForTeam(state.Team), state.Position)) steppedOut += 1;
                    bool idle = !state.Tagged && state.CanAct() && state.CastLockLeft <= 0.0 && state.Position.DistanceSquaredTo(before[id]) < 0.000001;
                    still[id] = idle ? still.GetValueOrDefault(id) + 1 : 0;
                    longest = System.Math.Max(longest, still[id]);
                }
            }
            Assert.AreEqual(0, steppedOut, "a holder stays in the base it holds");
            Assert.Less(longest, 45, $"no free bot stands still for a second and a half (the longest was {longest} ticks)");
        }

        [Test]
        public void ABotMatchOfTheCoachesLeavesTheBotsRealPlayersNotDummies()
        {
            // test_a_practice_match_vs_bots_has_three_real_players_a_side, without the scene: coach bots are no training dummies
            MatchSim sim = Sim();
            Enemies(sim);
            Allies(sim);
            foreach (int id in sim.Players.KeyList()) Assert.IsFalse(sim.IsFighter(id));
            Assert.AreEqual(0, sim.Brains.Count);
        }
    }
}
