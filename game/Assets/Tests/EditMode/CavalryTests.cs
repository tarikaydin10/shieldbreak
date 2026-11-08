using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Cavalry (M4 task 6): the horse is an agent with inertia, rider and horse are one unit. The couched lance strikes
    /// with the closing speed (ContactTrace); a horse runs men down with its weight, unless a braced spear or a shield
    /// wall stops it. Bots answer a charge with the same grammar and the same reaction time as a sword.
    /// </summary>
    public class CavalryTests
    {
        const ushort Rider = 0, Foot = 1;
        const int L1 = 1, L3 = 3, L5 = 5;
        static readonly float2 East = new float2(1f, 0f), West = new float2(-1f, 0f);

        CombatSetup setup;

        [SetUp]
        public void SetUp() => setup = TestCombat.Setup();

        [TearDown]
        public void TearDown()
        {
            setup?.Dispose();
            setup = null;
        }

        SimWorld World()
        {
            var config = TestScenarios.Config(1, 96, 64);
            config.InputDelayTicks = 1;
            config.MaxFormations = 4;
            config.Formations = TestScenarios.Formations;
            return new SimWorld(config, setup);
        }

        /// <summary>A rider (lance drawn) controlled by player 0, standing at <paramref name="x"/>, facing east.</summary>
        static int SpawnRider(SimWorld world, float x, int bot = 0)
        {
            int id = world.SpawnAgent(0, new float2(x, 48f), TestScenarios.AgentRadius, East, TestCombat.Lance, bot);
            world.SetMounted(id);
            if (bot == 0) world.AssignPlayerAgent(Rider, id);
            return id;
        }

        static void Ride(SimWorld world, int rider, float2 direction) =>
            world.Submit(Rider, InputCommandType.PlayerMove, new int4(rider, 0, 0, 0), new float4(direction, 0f, 0f));

        static void Couch(SimWorld world, int rider) =>
            world.Submit(Rider, InputCommandType.CombatAttack, new int4(rider, AttackDirection.Thrust, 0, 0));

        static List<CombatEvent> Run(SimWorld world, int ticks, List<CombatEvent> log = null)
        {
            log ??= new List<CombatEvent>();
            for (int t = 0; t < ticks; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents) log.Add(e);
            }
            return log;
        }

        static float Speed(SimWorld world, int id) => math.length(world.Agents[id].Velocity);

        // ---- The horse ----

        [Test]
        public void AHorse_OutrunsAMan_ButNeedsTimeToGetThere()
        {
            using var world = World();
            int horse = world.SpawnAgent(0, new float2(10f, 40f), TestScenarios.AgentRadius, East);
            world.SetMounted(horse);
            int man = world.SpawnAgent(0, new float2(10f, 56f), TestScenarios.AgentRadius, East);
            world.Submit(Rider, InputCommandType.SetTargetRange, new int4(horse, 1, 0, 0), new float4(90f, 40f, 0f, 0f));
            world.Submit(Rider, InputCommandType.SetTargetRange, new int4(man, 1, 0, 0), new float4(90f, 56f, 0f, 0f));
            Run(world, 31);
            Assert.That(Speed(world, man), Is.EqualTo(1.6f).Within(0.05f), "a man is at his pace within half a second");
            Assert.That(Speed(world, horse), Is.LessThan(0.3f * 9f), "a horse is not");
            Run(world, 240);
            Assert.That(Speed(world, horse), Is.GreaterThan(8.5f), "after four seconds it gallops");
        }

        [Test]
        public void AHorse_TurnsInACircle_NotOnTheSpot()
        {
            using var world = World();
            int horse = SpawnRider(world, 10f);
            Ride(world, horse, East);
            Run(world, 300);
            Assert.That(Speed(world, horse), Is.GreaterThan(8.5f));
            Ride(world, horse, West);
            Run(world, 30);
            Assert.That(world.Agents[horse].Velocity.x, Is.GreaterThan(0f), "half a second after turning back it still runs on");
            float2 start = world.Agents[horse].Position;
            Run(world, 360);
            Assert.That(world.Agents[horse].Velocity.x, Is.LessThan(-4f), "in the end it comes back");
            Assert.That(math.abs(world.Agents[horse].Position.y - 48f), Is.GreaterThan(3f), "round a circle, not back along its own track");
            Assert.That(start.x, Is.GreaterThan(10f));
        }

        [Test]
        public void TheForest_SlowsAHorseMoreThanAMan()
        {
            using var world = World();
            world.PaintArea(new float2(48f, 48f), 60f, GroundKind.Forest);
            int horse = SpawnRider(world, 10f);
            Ride(world, horse, East);
            Run(world, 360);
            Assert.That(Speed(world, horse), Is.EqualTo(9f * 0.45f).Within(0.3f));
        }

        // ---- The couched lance (ContactTrace) ----

        /// <summary>Rides at a man standing at x = 60 and couches the lance 12 m before him.</summary>
        List<CombatEvent> Charge(SimWorld world, int rider, bool couch = true, int ticks = 600, bool trace = false)
        {
            var log = new List<CombatEvent>();
            Ride(world, rider, East);
            bool couched = !couch;
            for (int t = 0; t < ticks; t++)
            {
                if (!couched && world.Agents[rider].Position.x > 48f)
                {
                    Couch(world, rider);
                    couched = true;
                }
                world.Step();
                foreach (var e in world.LastEvents) log.Add(e);
                var c = world.Combat[rider];
                if (trace && world.Agents[rider].Position.x > 47f && world.Agents[rider].Position.x < 60f)
                    TestContext.WriteLine($"t{world.Tick} x {world.Agents[rider].Position.x:0.00} v {world.Agents[rider].Velocity.x:0.0},{world.Agents[rider].Velocity.y:0.0} f {world.Agents[rider].Facing.x:0.00} state {c.State} st {c.StateTick} hits {c.HitCount} w {c.WeaponId} ev {string.Join(",", world.LastEvents.ToArray().Select(e => e.Type))}");
            }
            return log;
        }

        [Test]
        public void ACouchedLance_AtAGallop_KillsAStandingMan_BeforeTheHorseReachesHim()
        {
            using var world = World();
            int rider = SpawnRider(world, 6f);
            int man = world.SpawnAgent(1, new float2(60f, 48f), TestScenarios.AgentRadius, West, TestCombat.OneHand);
            var log = Charge(world, rider);
            var first = log.First(e => e.Target == man && (e.Type == CombatEventType.Hit || e.Type == CombatEventType.Trample));
            TestContext.WriteLine($"lance: {first.Energy:0} J, {first.Damage:0} damage");
            Assert.That(first.Type, Is.EqualTo(CombatEventType.Hit), "the lance strikes first, then the horse");
            Assert.That(world.Agents[man].IsDead, $"a gallop into a standing man kills ({first.Damage:0} damage)");
        }

        [Test]
        public void ACouchedLance_AtAStand_DoesNothing()
        {
            using var world = World();
            int rider = SpawnRider(world, 57f);
            int man = world.SpawnAgent(1, new float2(60f, 48f), TestScenarios.AgentRadius, West, TestCombat.OneHand);
            Couch(world, rider);
            var log = Run(world, 150);
            Assert.That(log.Any(e => e.Type == CombatEventType.AttackStart && e.Attacker == rider), "the lance went down");
            Assert.That(log.Any(e => e.Target == man && e.Type == CombatEventType.Hit), Is.False, "but without speed it only touches him");
            Assert.That(world.Combat[man].Health, Is.EqualTo(setup.Tuning.HealthMax));
        }

        // ---- Weight in the line ----

        [Test]
        public void AHorse_RunsDownAManInItsWay_AndLosesSpeed()
        {
            using var world = World();
            int rider = SpawnRider(world, 6f);
            int man = world.SpawnAgent(1, new float2(60f, 48f), TestScenarios.AgentRadius, West, TestCombat.OneHand);
            Ride(world, rider, East);
            float before = 0f;
            int trampled = -1;
            for (int t = 0; t < 600 && trampled < 0; t++)
            {
                before = Speed(world, rider);
                world.Step();
                foreach (var e in world.LastEvents) if (e.Type == CombatEventType.Trample && e.Target == man) trampled = world.Tick;
            }
            Assert.That(trampled, Is.GreaterThan(0), "the horse ran into him");
            Assert.That(world.Combat[man].Health, Is.LessThan(setup.Tuning.HealthMax), "and hurt him");
            Assert.That(math.length(world.Combat[man].Knockback), Is.GreaterThan(1f), "and threw him back");
            Assert.That(Speed(world, rider), Is.LessThan(before * 0.9f), "it costs the horse speed");
            Assert.That(Speed(world, rider), Is.GreaterThan(before * 0.5f), "but one man does not stop it");
        }

        [Test]
        public void ABracedSpear_StopsTheHorse_AndTheRiderPaysForIt()
        {
            using var world = World();
            int rider = SpawnRider(world, 6f);
            int spear = world.SpawnAgent(1, new float2(60f, 48f), TestScenarios.AgentRadius, West, TestCombat.Polearm);
            world.AssignPlayerAgent(Foot, spear);
            world.Submit(Foot, InputCommandType.CombatBlock, new int4(spear, 1, 0, 0));
            var log = Charge(world, rider, couch: false);
            Assert.That(log.Any(e => e.Type == CombatEventType.Brace && e.Attacker == spear && e.Target == rider), "the horse ran onto the spear");
            Assert.That(world.Agents[rider].IsDead, "at a gallop that kills the rider");
            Assert.That(world.Combat[spear].Health, Is.EqualTo(setup.Tuning.HealthMax), "the spearman stands");
        }

        [Test]
        public void ASpear_NotBraced_IsRunDownLikeAnyMan()
        {
            using var world = World();
            int rider = SpawnRider(world, 6f);
            int spear = world.SpawnAgent(1, new float2(60f, 48f), TestScenarios.AgentRadius, West, TestCombat.Polearm);
            var log = Charge(world, rider, couch: false);
            Assert.That(log.Any(e => e.Type == CombatEventType.Brace), Is.False);
            Assert.That(log.Any(e => e.Type == CombatEventType.Trample && e.Target == spear));
            Assert.That(world.Agents[rider].IsDead, Is.False);
        }

        float RiderSpeedAfter(FormationType type, out List<CombatEvent> log, out int first, out int count)
        {
            var world = World();
            try
            {
                int rider = SpawnRider(world, 6f);
                first = world.AgentCount;
                count = 8;
                world.SpawnFormation(1, Foot, type, 8, count, new float2(60f, 48f), West, TestScenarios.AgentRadius, TestCombat.OneHand, L1);
                log = Charge(world, rider, couch: false, ticks: 520);
                return Speed(world, rider);
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void AShieldWall_MakesTheHorseRefuse_ALineOfShieldsIsRunOver()
        {
            float wallSpeed = RiderSpeedAfter(FormationType.ShieldWall, out var wallLog, out int wallFirst, out int wallCount);
            Assert.That(wallLog.Any(e => e.Type == CombatEventType.Refuse), $"the horse refuses the wall; events: {string.Join(",", wallLog.Select(e => e.Type).Distinct())}");
            Assert.That(wallLog.Any(e => e.Type == CombatEventType.Death && e.Target >= wallFirst && e.Target < wallFirst + wallCount), Is.False, "nobody in the wall dies");

            float lineSpeed = RiderSpeedAfter(FormationType.Line, out var lineLog, out _, out _);
            Assert.That(lineLog.Any(e => e.Type == CombatEventType.Refuse), Is.False);
            Assert.That(lineLog.Any(e => e.Type == CombatEventType.Trample), "a line of raised shields is run over");
            TestContext.WriteLine($"rider speed at the end: wall {wallSpeed:0.0} m/s, line {lineSpeed:0.0} m/s");
        }

        // ---- Bots ----

        [Test]
        public void ASpearman_SeesTheRiderComing_AndBraces()
        {
            using var world = World();
            int rider = SpawnRider(world, 6f);
            int spear = world.SpawnAgent(1, new float2(60f, 48f), TestScenarios.AgentRadius, West, TestCombat.Polearm, L5);
            var log = Charge(world, rider, couch: false);
            Assert.That(log.Any(e => e.Type == CombatEventType.Brace && e.Attacker == spear), "he braced in time");
            Assert.That(world.Agents[rider].IsDead);
        }

        [Test]
        public void ALine_DoesNotSeeARiderBehindItsBack()
        {
            using var world = World();
            int rider = SpawnRider(world, 6f);
            int first = world.AgentCount;
            world.SpawnFormation(1, Foot, FormationType.Line, 6, 6, new float2(60f, 50.75f), East, TestScenarios.AgentRadius, TestCombat.Polearm, L5);
            var log = Charge(world, rider, couch: false, ticks: 450);
            Assert.That(log.Any(e => e.Type == CombatEventType.Brace), Is.False, "spears facing the other way brace against nothing");
            Assert.That(log.Any(e => e.Type == CombatEventType.Trample && e.Target >= first), "a charge in the back runs them down");
        }

        [Test]
        public void ARiderBot_ChargesPassAfterPass()
        {
            using var world = World();
            int rider = SpawnRider(world, 16f, L3);
            var men = new[]
            {
                world.SpawnAgent(1, new float2(60f, 40f), TestScenarios.AgentRadius, West, TestCombat.OneHand),
                world.SpawnAgent(1, new float2(60f, 56f), TestScenarios.AgentRadius, West, TestCombat.OneHand),
                world.SpawnAgent(1, new float2(72f, 48f), TestScenarios.AgentRadius, West, TestCombat.OneHand),
            };
            var log = Run(world, 60 * 60);
            int couches = log.Count(e => e.Type == CombatEventType.AttackStart && e.Attacker == rider);
            int passes = world.Combat[rider].RidePhase >> 1;
            int dead = men.Count(m => world.Agents[m].IsDead);
            TestContext.WriteLine($"couches {couches}, passes {passes}, dead {dead}");
            Assert.That(couches, Is.GreaterThanOrEqualTo(2), "it lowered the lance for more than one charge");
            Assert.That(passes, Is.GreaterThanOrEqualTo(2), "it rode out and came back");
            Assert.That(dead, Is.GreaterThanOrEqualTo(2), "and men who stand still against cavalry die");
        }

        [Test]
        public void Cavalry_IsDeterministic()
        {
            ulong Play()
            {
                using var world = World();
                world.SpawnFormation(0, Rider, FormationType.Line, 4, 8, new float2(20f, 48f), East, TestScenarios.AgentRadius, TestCombat.Lance, L3, -1, mounted: true);
                int f = world.SpawnFormation(1, Foot, FormationType.ShieldWall, 6, 12, new float2(70f, 48f), West, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
                world.SpawnFormation(1, Foot, FormationType.Line, 6, 12, new float2(70f, 30f), West, TestScenarios.AgentRadius, TestCombat.Polearm, L3);
                world.ApplyFormationOrder(FormationOrder.Charge(0), Rider);
                Run(world, 1800);
                return world.ComputeHash();
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }
    }
}
